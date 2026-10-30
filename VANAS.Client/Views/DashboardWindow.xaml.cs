using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VANAS.Client.Controls;
using VANAS.Client.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Client.Views
{
    public partial class DashboardWindow : Window
    {
        private record AvatarChoice(string Key, Brush Brush);
        private record HistoryRow(string When, string Station, string Duration, string Reason);

        private record GameTileVm(CatalogGameDto Game, string Title, string LauncherName, string Status, double Opacity, bool Installed);

        private readonly DispatcherTimer _gamesTimer = new() { Interval = TimeSpan.FromSeconds(20) };
        private List<CatalogGameDto> _catalog = new();
        private DetectionResult? _detection;
        private string _gamesSignature = string.Empty;
        private readonly double _initialMinutes;
        private readonly OverlayWindow _overlay = new();
        private readonly DispatcherTimer _bannerTimer = new() { Interval = TimeSpan.FromSeconds(10) };
        private readonly string _username;
        private bool _leaving;
        private bool _allowClose;
        private string _selectedAvatar = Avatars.Default;
        private string _displayName;

        public DashboardWindow(string username, double initialMinutes)
        {
            InitializeComponent();
            _username = username;
            _displayName = username;
            lblUser.Text = username.ToUpper();
            avatarSmall.Set(Avatars.Default, username);
            _initialMinutes = Math.Max(initialMinutes, 1);

            listAvatars.ItemsSource = Avatars.Colors.Select(c => new AvatarChoice(c.Key, AvatarBadge.BrushFor(c.Key))).ToList();

            App.Session.TimeChanged += OnTimeChanged;
            App.Session.OnlineChanged += OnOnlineChanged;
            App.Session.Ended += OnEnded;
            App.Session.TimeWarning += OnTimeWarning;

            // The overlay shows the clock over games, and gets out of the way while this window is in front.
            Activated += (_, _) => _overlay.Hide();
            Deactivated += (_, _) => { if (!_leaving) _overlay.Show(); };
            _bannerTimer.Tick += (_, _) =>
            {
                _bannerTimer.Stop();
                bannerWarning.Visibility = Visibility.Collapsed;
            };

            _gamesTimer.Tick += async (_, _) => await RefreshGamesAsync();
            Loaded += async (_, _) =>
            {
                Kiosk.EnterSession(this);
                _gamesTimer.Start();
                await Task.WhenAll(LoadProfileAsync(), RefreshGamesAsync());
            };
            Closing += (_, e) => e.Cancel = Kiosk.Enabled && !_allowClose && !App.Exiting;
            Closed += (_, _) =>
            {
                Unsubscribe();
                _bannerTimer.Stop();
                _gamesTimer.Stop();
                _overlay.Close();
            };
            OnTimeChanged(App.Session.MinutesLeft);
        }

        private void Unsubscribe()
        {
            App.Session.TimeChanged -= OnTimeChanged;
            App.Session.OnlineChanged -= OnOnlineChanged;
            App.Session.Ended -= OnEnded;
            App.Session.TimeWarning -= OnTimeWarning;
        }

        // ---------- Time ----------

        private static string FormatTime(double minutes)
        {
            var t = TimeSpan.FromMinutes(minutes);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private void OnTimeChanged(double minutes)
        {
            var text = FormatTime(minutes);
            lblTime.Text = text;
            _overlay.SetTime(text);
            barTime.Value = Math.Clamp(minutes / _initialMinutes, 0, 1);
        }

        private void OnTimeWarning(int minutes)
        {
            var message = minutes == 1 ? "Il vous reste 1 minute !" : $"Il vous reste {minutes} minutes";
            lblWarning.Text = message;
            bannerWarning.Visibility = Visibility.Visible;
            _bannerTimer.Stop();
            _bannerTimer.Start();
            _overlay.ShowWarning(message);
        }

        private void OnOnlineChanged(bool online)
        {
            lblNet.Text = online ? "● EN LIGNE" : "● HORS LIGNE - RECONNEXION...";
            lblNet.Foreground = (Brush)FindResource(online ? "OkBrush" : "DangerBrush");
        }

        private void OnEnded(SessionEndedReason reason)
        {
            var message = reason switch
            {
                SessionEndedReason.TimeExpired => "Temps écoulé. Passez au comptoir pour recharger.",
                SessionEndedReason.Kicked => "Session fermée par le staff.",
                SessionEndedReason.OfflineTimeout => "Connexion au serveur perdue. Session verrouillée.",
                _ => "Session terminée."
            };
            ReturnToLogin(message);
        }

        private async void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            _leaving = true;
            await App.Session.StopAsync();
            ReturnToLogin(null);
        }

        private void ReturnToLogin(string? notice)
        {
            if (_leaving && notice != null) return;
            _leaving = true;
            _overlay.Hide();
            App.Token = null;
            new LoginWindow(notice).Show();
            _allowClose = true;
            if (Kiosk.Enabled) _ = Task.Run(() => ProcessCleaner.KillStartedSince(App.SessionStartedAt));
            Close();
        }

        // ---------- Tabs ----------

        private void tabGames_Click(object sender, RoutedEventArgs e) => ShowTab(profile: false);

        private async void tabProfile_Click(object sender, RoutedEventArgs e)
        {
            ShowTab(profile: true);
            await LoadProfileAsync();
            await LoadHistoryAsync();
        }

        private void ShowTab(bool profile)
        {
            panelGames.Visibility = profile ? Visibility.Collapsed : Visibility.Visible;
            panelProfile.Visibility = profile ? Visibility.Visible : Visibility.Collapsed;
            underGames.Visibility = profile ? Visibility.Hidden : Visibility.Visible;
            underProfile.Visibility = profile ? Visibility.Visible : Visibility.Hidden;
        }

        // ---------- Profile ----------

        private async Task LoadProfileAsync()
        {
            try
            {
                var response = await App.Api.GetProfile();
                if (!response.IsSuccessStatusCode || response.Content == null) return;
                var p = response.Content;

                _displayName = p.DisplayName;
                _selectedAvatar = p.AvatarKey;
                lblUser.Text = p.DisplayName.ToUpper();
                avatarSmall.Set(p.AvatarKey, p.DisplayName);
                avatarBig.Set(p.AvatarKey, p.DisplayName);
                if (!txtPseudo.IsKeyboardFocused) txtPseudo.Text = p.DisplayName;

                var played = TimeSpan.FromMinutes(p.TotalMinutesPlayed);
                statPlayed.Text = played.TotalHours >= 1 ? $"{(int)played.TotalHours} h {played.Minutes:D2}" : $"{(int)played.TotalMinutes} min";
                statSessions.Text = p.SessionCount.ToString(CultureInfo.CurrentCulture);
                statSince.Text = p.MemberSince.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
            }
            catch
            {
                // Profile is a nicety: the session keeps working without it.
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var response = await App.Api.GetSessionHistory(20);
                if (!response.IsSuccessStatusCode || response.Content == null) return;

                var rows = response.Content.Select(s => new HistoryRow(
                    s.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                    s.StationName,
                    FormatMinutes(s.MinutesConsumed),
                    s.EndedAt == null ? "en cours" : s.EndReason switch
                    {
                        SessionEndReason.TimeExpired => "temps écoulé",
                        SessionEndReason.Kicked => "fermée par le staff",
                        SessionEndReason.Replaced => "remplacée",
                        _ => "déconnexion"
                    })).ToList();

                listHistory.ItemsSource = rows;
                lblNoHistory.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                // see LoadProfileAsync
            }
        }

        private static string FormatMinutes(double minutes)
        {
            var t = TimeSpan.FromMinutes(minutes);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:D2}" : $"{(int)t.TotalMinutes} min {t.Seconds:D2} s";
        }

        private void AvatarChoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not string key) return;
            _selectedAvatar = key;
            avatarBig.Set(key, txtPseudo.Text.Length > 0 ? txtPseudo.Text : _displayName);
        }

        private async void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            lblProfileStatus.Foreground = (Brush)FindResource("MutedBrush");
            lblProfileStatus.Text = "Enregistrement...";
            try
            {
                var response = await App.Api.UpdateProfile(new UpdateProfileRequest(txtPseudo.Text, _selectedAvatar));
                if (response.IsSuccessStatusCode)
                {
                    lblProfileStatus.Foreground = (Brush)FindResource("OkBrush");
                    lblProfileStatus.Text = "Profil enregistré.";
                    await LoadProfileAsync();
                }
                else
                {
                    lblProfileStatus.Foreground = (Brush)FindResource("DangerBrush");
                    lblProfileStatus.Text = ApiErrors.Describe(response);
                }
            }
            catch (Exception ex)
            {
                lblProfileStatus.Foreground = (Brush)FindResource("DangerBrush");
                lblProfileStatus.Text = ApiErrors.Describe(ex);
            }
        }

        // ---------- Games ----------

        /// <summary>Re-reads the server catalog and what is installed on this PC (cheap; runs every 20 s).</summary>
        private async Task RefreshGamesAsync()
        {
            try
            {
                var catalog = await App.Api.GetCatalog();
                if (catalog.IsSuccessStatusCode && catalog.Content != null) _catalog = catalog.Content;
            }
            catch
            {
                // keep the last known catalog while the server is unreachable
            }

            _detection = await Task.Run(LauncherDetector.Detect);
            ApplyGameFilter();
        }

        private void ApplyGameFilter()
        {
            if (_detection == null) return;
            var detection = _detection;
            var search = txtSearch.Text.Trim();

            var tiles = _catalog
                .Where(g => search.Length == 0 || g.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(g =>
                {
                    var installed = detection.IsInstalled(g.Launcher, g.ExternalId);
                    return new GameTileVm(g, g.Title, GameLauncherService.LauncherName(g.Launcher),
                        installed ? "JOUER" : "NON INSTALLÉ", installed ? 1.0 : 0.55, installed);
                })
                .OrderByDescending(t => t.Installed).ThenBy(t => t.Title)
                .ToList();

            // Avoid rebuilding the grid (and resetting hover/scroll) when nothing changed.
            var signature = string.Join('|', tiles.Select(t => $"{t.Game.Id}:{t.Installed}:{t.Title}"));
            if (signature == _gamesSignature) return;
            _gamesSignature = signature;

            listGames.ItemsSource = tiles;
            lblNoGames.Visibility = tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyGameFilter();

        private void ShowGameMessage(string? message, bool isError)
        {
            lblGameMessage.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
            lblGameMessage.Text = message ?? string.Empty;
            lblGameMessage.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "OkBrush");
        }

        private void Game_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not GameTileVm tile || _detection == null) return;

            if (!tile.Installed)
            {
                ShowGameMessage($"{tile.Title} n'est pas installé sur ce poste.", isError: true);
                return;
            }

            var error = GameLauncherService.Launch(tile.Game, _detection);
            ShowGameMessage(error ?? $"Lancement de {tile.Title}...", isError: error != null);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) => App.HandleExitKey(e, this);
    }
}
