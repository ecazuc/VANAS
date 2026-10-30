using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VANAS.Client.Services;
using VANAS.Shared.Models;

namespace VANAS.Client.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly double _initialMinutes;
        private bool _leaving;

        public DashboardWindow(string username, double initialMinutes)
        {
            InitializeComponent();
            lblUser.Text = username.ToUpper();
            _initialMinutes = Math.Max(initialMinutes, 1);

            // Placeholders: real detection of installed launchers/games arrives in PC-4.
            listLaunchers.ItemsSource = new List<GameItem>
            {
                new() { Title = "STEAM", ExePath = "steam://open/main" },
                new() { Title = "EPIC GAMES", ExePath = "com.epicgames.launcher://" }
            };

            App.Session.TimeChanged += OnTimeChanged;
            App.Session.OnlineChanged += OnOnlineChanged;
            App.Session.Ended += OnEnded;
            Closed += (_, _) => Unsubscribe();
            OnTimeChanged(App.Session.MinutesLeft);
        }

        private void Unsubscribe()
        {
            App.Session.TimeChanged -= OnTimeChanged;
            App.Session.OnlineChanged -= OnOnlineChanged;
            App.Session.Ended -= OnEnded;
        }

        private void OnTimeChanged(double minutes)
        {
            var t = TimeSpan.FromMinutes(minutes);
            lblTime.Text = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            barTime.Value = Math.Clamp(minutes / _initialMinutes, 0, 1);
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
            App.Token = null;
            new LoginWindow(notice).Show();
            Close();
        }

        private void Launcher_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not GameItem game) return;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = game.ExePath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de lancer {game.Title} : {ex.Message}");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) => App.HandleExitKey(e);
    }
}
