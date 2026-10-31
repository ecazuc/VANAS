using Microsoft.AspNetCore.SignalR.Client;
using VANAS.Mobile.Services;
using VANAS.Shared.Contracts;

namespace VANAS.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(15);

    private readonly ApiService _api;
    private readonly TimeWarningTracker _warnings = new();

    private IDispatcherTimer? _timer;
    private HubConnection? _hub;
    private double _baseMinutes;
    private bool _inSession;
    private DateTime _baseAt = DateTime.UtcNow;
    private DateTime _lastPoll = DateTime.MinValue;
    private bool _firstLoad = true;
    private bool _loading;

    public HomePage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _firstLoad = true;
        await LoadProfileAsync();
        await RefreshLiveAsync();
        await LoadHistoryAsync();
        StartTimer();
        await ConnectHubAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        await DisconnectHubAsync();
    }

    // ----- Data -----

    private async Task LoadProfileAsync()
    {
        var result = await _api.GetProfileAsync();
        if (!result.Ok || result.Value == null)
        {
            // An expired session already cleared the token: go back to the login page.
            if (!_api.IsSignedIn)
            {
                await Shell.Current.GoToAsync("//login");
                return;
            }

            ShowError(result.Error);
            return;
        }

        ShowError(null);
        var p = result.Value;
        NameLabel.Text = p.DisplayName;
        AccountLabel.Text = $"Compte {p.Username}";
        PlayedLabel.Text = Format(p.TotalMinutesPlayed);
        SessionsLabel.Text = $"{p.SessionCount} session(s)";
        ConsentBox.IsVisible = _api.ConsentPending;
        PassLabel.Text = p.PassSummary == null ? null : "FORFAIT · " + p.PassSummary;
        PassLabel.IsVisible = p.PassSummary != null;
    }

    private async Task RefreshLiveAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            await _api.RefreshAccountAsync();
            ConsentBox.IsVisible = _api.ConsentPending;
            var result = await _api.GetLiveAsync();
            if (!result.Ok || result.Value == null)
            {
                if (!_api.IsSignedIn) await Shell.Current.GoToAsync("//login");
                else ShowError(result.Error);
                return;
            }

            ShowError(null);
            var live = result.Value;
            _baseMinutes = live.MinutesLeft;
            _baseAt = DateTime.UtcNow;
            _inSession = live.InSession;
            _lastPoll = DateTime.UtcNow;

            if (_firstLoad)
            {
                _warnings.Reset(live.MinutesLeft);
                _firstLoad = false;
            }

            StateLabel.Text = live.InSession ? "● EN JEU" : "○ HORS SESSION";
            StateLabel.TextColor = Color.FromArgb(live.InSession ? "#3DF2A5" : "#7C828F");
            LinkLabel.Text = live.InSession ? $"Connecté sur {live.StationName}" : "Le décompte démarre quand vous vous connectez à un PC du café.";
            Render();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadHistoryAsync()
    {
        var result = await _api.GetSessionsAsync();
        HistoryBox.Children.Clear();
        var items = result.Value ?? new List<SessionHistoryItem>();
        NoHistoryLabel.IsVisible = items.Count == 0;

        foreach (var s in items)
        {
            HistoryBox.Children.Add(new Label
            {
                Text = $"{s.StartedAt.ToLocalTime():dd/MM HH:mm}  ·  {s.StationName}  ·  {Format(s.MinutesConsumed)}",
                TextColor = Color.FromArgb("#C8CCD4"),
                FontSize = 13
            });
        }
    }

    // ----- Countdown -----

    /// <summary>While the player is on a PC the server decrements the balance; the phone counts down between two refreshes.</summary>
    private double CurrentMinutes =>
        _inSession ? Math.Max(0, _baseMinutes - (DateTime.UtcNow - _baseAt).TotalMinutes) : _baseMinutes;

    private void StartTimer()
    {
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        Render();
        if (DateTime.UtcNow - _lastPoll > PollEvery) await RefreshLiveAsync();
    }

    private void Render()
    {
        var minutes = CurrentMinutes;
        var totalSeconds = (int)Math.Max(0, minutes * 60);
        TimeLabel.Text = totalSeconds >= 3600
            ? $"{totalSeconds / 3600} h {totalSeconds % 3600 / 60:00} min"
            : $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        if (!_inSession)
        {
            WarningBox.IsVisible = false;
            return;
        }

        if (_warnings.Check(minutes) is int threshold)
        {
            WarningLabel.Text = threshold == 1 ? "Il vous reste moins d'1 minute !" : $"Il vous reste {threshold} minutes.";
            WarningBox.IsVisible = true;
        }
    }

    // ----- Live connection -----

    /// <summary>The server pushes "Refresh" when staff add time or kick the player; the phone then re-reads the balance.</summary>
    private async Task ConnectHubAsync()
    {
        await DisconnectHubAsync();
        if (_api.Token == null) return;

        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(_api.ServerUrl + "/hubs/session", o => o.AccessTokenProvider = () => Task.FromResult(_api.Token))
                .WithAutomaticReconnect()
                .Build();
            _hub.On("Refresh", () => MainThread.BeginInvokeOnMainThread(async () =>
            {
                _firstLoad = false;
                await RefreshLiveAsync();
            }));
            await _hub.StartAsync();
        }
        catch (Exception)
        {
            // Without the live channel the 15-second refresh still keeps the screen up to date.
        }
    }

    private async Task DisconnectHubAsync()
    {
        var hub = _hub;
        _hub = null;
        if (hub == null) return;
        try { await hub.DisposeAsync(); } catch (Exception) { /* closing anyway */ }
    }

    // ----- UI helpers -----

    private async void OnSignOut(object? sender, EventArgs e)
    {
        _timer?.Stop();
        await DisconnectHubAsync();
        _api.SignOut();
        await Shell.Current.GoToAsync("//login");
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = message != null;
    }

    private static string Format(double minutes)
    {
        var total = (int)Math.Round(Math.Max(0, minutes));
        return total >= 60 ? $"{total / 60} h {total % 60:00} min" : $"{total} min";
    }
}
