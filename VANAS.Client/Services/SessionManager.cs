using System.Net;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows.Threading;
using VANAS.Client.Interfaces;
using VANAS.Shared.Contracts;

namespace VANAS.Client.Services
{
    public enum SessionEndedReason
    {
        TimeExpired,
        Kicked,
        SessionLost,
        OfflineTimeout
    }

    /// <summary>
    /// Keeps the server informed (heartbeat) and drives the on-screen countdown.
    /// The server is the source of truth; between heartbeats the countdown is interpolated locally.
    /// </summary>
    public class SessionManager
    {
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan OfflineGrace = TimeSpan.FromMinutes(2);

        private readonly IGamingApi _api;
        private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _beat = new() { Interval = HeartbeatInterval };

        private int _sessionId;
        private double _serverMinutes;
        private DateTime _syncedAt;
        private DateTime? _offlineSince;
        private bool _busy;
        private readonly TimeWarningTracker _warnings = new();
        private bool _syncAgain;
        private bool _running;
        private HubConnection? _hub;

        public SessionManager(IGamingApi api)
        {
            _api = api;
            _tick.Tick += (_, _) => OnTick();
            _beat.Tick += async (_, _) => await HeartbeatAsync();
        }

        public bool IsOnline => _offlineSince == null;

        public event Action<double>? TimeChanged;
        public event Action<bool>? OnlineChanged;
        public event Action<SessionEndedReason>? Ended;

        /// <summary>Raised once when 10, 5 and 1 minute(s) remain (re-armed if staff adds time).</summary>
        public event Action<int>? TimeWarning;

        /// <summary>A message typed by staff in the console for this PC.</summary>
        public event Action<string>? StaffMessage;

        public double MinutesLeft => Math.Max(0, _serverMinutes - (DateTime.UtcNow - _syncedAt).TotalMinutes);

        public void Start(SessionInfo info)
        {
            _sessionId = info.SessionId;
            Sync(info.MinutesLeft);
            _warnings.Reset(info.MinutesLeft);
            _offlineSince = null;
            _running = true;
            _beat.Interval = HeartbeatInterval;
            _tick.Start();
            _beat.Start();
            TimeChanged?.Invoke(MinutesLeft);
            _ = ConnectHubAsync();
        }

        /// <summary>
        /// Instant push from the server (kick, added time). Best effort: without it the 30 s heartbeat still applies everything.
        /// </summary>
        private async Task ConnectHubAsync()
        {
            try
            {
                _hub = new HubConnectionBuilder()
                    .WithUrl(new Uri(new Uri(App.Config.ServerUrl), "/hubs/session"), o => o.AccessTokenProvider = () => Task.FromResult(App.Token))
                    .WithAutomaticReconnect()
                    .Build();
                _hub.On("Refresh", () => _ = Application.Current.Dispatcher.InvokeAsync(SyncNowAsync));
                await _hub.StartAsync();
            }
            catch
            {
                // Heartbeat polling covers us.
            }
        }

        private async Task SyncNowAsync()
        {
            if (_busy) _syncAgain = true;
            else await HeartbeatAsync();
        }

        private async Task CloseHubAsync()
        {
            var hub = _hub;
            _hub = null;
            if (hub == null) return;
            try { await hub.DisposeAsync(); } catch { /* closing anyway */ }
        }

        /// <summary>Ends the session on the server (best effort) and stops all timers.</summary>
        public async Task StopAsync()
        {
            if (!_running) return;
            Halt();
            try { await _api.EndSession(_sessionId); } catch { /* the server will stop billing after the heartbeat gap anyway */ }
        }

        private void Halt()
        {
            _running = false;
            _tick.Stop();
            _ = CloseHubAsync();
            _beat.Stop();
        }

        private void Sync(double minutes)
        {
            _serverMinutes = minutes;
            _syncedAt = DateTime.UtcNow;
        }

        private async void OnTick()
        {
            var left = MinutesLeft;
            TimeChanged?.Invoke(left);
            if (left > 0 && _running && _warnings.Check(left) is int threshold) TimeWarning?.Invoke(threshold);

            if (left <= 0 && _running)
            {
                // Let the server confirm; if unreachable the offline path ends the session.
                if (IsOnline) await HeartbeatAsync();
                else Finish(SessionEndedReason.TimeExpired);
            }
        }

        private async Task HeartbeatAsync()
        {
            if (_busy || !_running) return;
            _busy = true;
            try
            {
                var response = await _api.Heartbeat(_sessionId);

                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
                {
                    Finish(SessionEndedReason.SessionLost);
                    return;
                }
                if (!response.IsSuccessStatusCode || response.Content == null)
                {
                    GoOffline();
                    return;
                }

                GoOnline();
                Sync(response.Content.MinutesLeft);
                TimeChanged?.Invoke(MinutesLeft);

                if (!string.IsNullOrWhiteSpace(response.Content.StaffMessage)) StaffMessage?.Invoke(response.Content.StaffMessage);

                switch (response.Content.Status)
                {
                    case SessionStatus.Kicked: Finish(SessionEndedReason.Kicked); break;
                    case SessionStatus.TimeExpired: Finish(SessionEndedReason.TimeExpired); break;
                }
            }
            catch
            {
                GoOffline();
            }
            finally
            {
                _busy = false;
                if (_syncAgain && _running)
                {
                    _syncAgain = false;
                    _ = HeartbeatAsync();
                }
            }
        }

        private void GoOffline()
        {
            if (_offlineSince == null)
            {
                _offlineSince = DateTime.UtcNow;
                _beat.Interval = RetryInterval;
                OnlineChanged?.Invoke(false);
            }
            else if (DateTime.UtcNow - _offlineSince > OfflineGrace)
            {
                Finish(SessionEndedReason.OfflineTimeout);
            }
        }

        private void GoOnline()
        {
            if (_offlineSince == null) return;
            _offlineSince = null;
            _beat.Interval = HeartbeatInterval;
            OnlineChanged?.Invoke(true);
        }

        private void Finish(SessionEndedReason reason)
        {
            if (!_running) return;
            Halt();
            Ended?.Invoke(reason);
        }
    }
}
