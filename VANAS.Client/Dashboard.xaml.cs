using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using VANAS.Shared.Models;

namespace VANAS.Client
{
    /// <summary>
    /// Logique d'interaction pour Dashboard.xaml
    /// </summary>
    public partial class Dashboard : Window
    {
        private double _minutesRemaining;
        private DispatcherTimer _timer;
        public ObservableCollection<GameItem> Games { get; set; }
        public Dashboard(string username, double initialMinutes)
        {
            InitializeComponent();
            lblWelcome.Text = $"PLAYER: {username.ToUpper()}";
            _minutesRemaining = initialMinutes;

            Games = new ObservableCollection<GameItem>
            {
            new GameItem { Title = "Steam", ExePath = "\"C:\\Program Files (x86)\\Steam\\steam.exe\"", IconPath = "/Assets/icon_steam_vr.png" },
            new GameItem { Title = "Riot Client", ExePath = "\"Q:\\Riot Games\\Riot Client\\RiotClientServices.exe\"", IconPath = "https://www.riotgames.com/darkroom/800/87521fcaeca5867538ae7f46ac152740:2f8144e17957078916e41d2410c111c3/002-rg-2021-full-lockup-offwhite.jpg" },
            new GameItem { Title = "League of Legends", ExePath = "calc.exe", IconPath = "https://example.com/lol.jpg" },
            new GameItem { Title = "Valorant", ExePath = "calc.exe", IconPath = "https://example.com/valorant.jpg" }
            };

            GamesList.ItemsSource = Games;

            // Start the countdown timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_minutesRemaining > 0)
            {
                _minutesRemaining -= (1.0 / 60.0); // Remove one second
                TimeSpan t = TimeSpan.FromMinutes(_minutesRemaining);
                lblTime.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
            }
            else
            {
                _timer.Stop();
                MessageBox.Show("Time is up! Logging out.");
                this.Close(); // In the future, we will return to the Login screen here
            }
        }

        private void GameTile_Click(object sender, RoutedEventArgs e)
        {
            // When a tile is clicked, we find out which "GameItem" it belongs to
            var button = sender as Button;
            var selectedGame = button?.DataContext as GameItem;

            if (selectedGame != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedGame.ExePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not launch {selectedGame.Title}: {ex.Message}");
                }
            }
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            // SECRET EXIT: Press Shift + F12 to close the app
            if (e.Key == System.Windows.Input.Key.F12 && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
            {
                Application.Current.Shutdown();
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop(); // Stop the countdown

            // Create a new instance of the login window
            AuthWindow loginWindow = new AuthWindow();
            loginWindow.Show();

            this.Close(); // Close the dashboard
        }

        

    }

}
