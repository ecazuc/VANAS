using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VANAS.Client.Services;
using VANAS.Shared.Contracts;

namespace VANAS.Client.Views
{
    public partial class LoginWindow : Window
    {
        private bool _registerMode;
        private bool _allowClose;

        public LoginWindow(string? notice = null)
        {
            InitializeComponent();
            lblStation.Text = $"POSTE : {App.Config.StationName.ToUpper()}";
            if (notice != null) ShowStatus(notice, isError: true);
            Loaded += (_, _) =>
            {
                txtUser.Focus();
                Kiosk.Lock(this);
            };
            Closing += (_, e) => e.Cancel = Kiosk.Enabled && !_allowClose && !App.Exiting;
            // The lock takes the focus back, except while the admin PIN window (owned by this one) is open: it needs the keyboard.
            Deactivated += (_, _) => { if (Kiosk.Enabled && !_allowClose && OwnedWindows.Count == 0) Dispatcher.BeginInvoke(Activate); };
        }

        private void ShowStatus(string text, bool isError)
        {
            lblStatus.Text = text;
            lblStatus.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "OkBrush");
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) => App.HandleExitKey(e, this);

        private void btnToggle_Click(object sender, RoutedEventArgs e)
        {
            _registerMode = !_registerMode;
            panelConfirm.Visibility = _registerMode ? Visibility.Visible : Visibility.Collapsed;
            lblMode.Text = _registerMode ? "CRÉATION DE COMPTE" : "ACCÈS JOUEUR";
            btnSubmit.Content = _registerMode ? "CRÉER LE COMPTE" : "CONNEXION";
            btnToggle.Content = _registerMode ? "Déjà un compte ? Se connecter" : "Pas de compte ? Créer un compte";
            lblStatus.Text = string.Empty;
        }

        private async void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            btnSubmit.IsEnabled = false;
            try
            {
                if (_registerMode) await RegisterAsync();
                else await LoginAsync();
            }
            catch (Exception ex)
            {
                ShowStatus(ApiErrors.Describe(ex), isError: true);
            }
            finally
            {
                btnSubmit.IsEnabled = true;
            }
        }

        private async Task RegisterAsync()
        {
            if (txtPass.Password != txtConfirm.Password)
            {
                ShowStatus("Les mots de passe ne correspondent pas.", isError: true);
                return;
            }

            if (!DateOnly.TryParseExact(txtBirth.Text.Trim(), "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var birth))
            {
                ShowStatus("Indiquez votre date de naissance au format JJ/MM/AAAA.", isError: true);
                return;
            }

            ShowStatus("Création du compte...", isError: false);
            var response = await App.Api.Register(new RegisterRequest(txtUser.Text, txtPass.Password, birth));
            if (!response.IsSuccessStatusCode)
            {
                ShowStatus(ApiErrors.Describe(response), isError: true);
                return;
            }

            var pending = response.Content?.ConsentPending == true;
            btnToggle_Click(this, new RoutedEventArgs());
            txtPass.Clear();
            txtConfirm.Clear();
            txtBirth.Clear();
            ShowStatus(pending
                ? "Compte créé. Un parent ou responsable légal doit passer au comptoir pour valider votre inscription."
                : "Compte créé. Demandez du temps au comptoir, puis connectez-vous.", isError: false);
        }

        private async Task LoginAsync()
        {
            ShowStatus("Connexion...", isError: false);
            var login = await App.Api.Login(new LoginRequest(txtUser.Text, txtPass.Password));
            if (!login.IsSuccessStatusCode || login.Content == null)
            {
                ShowStatus(ApiErrors.Describe(login), isError: true);
                return;
            }

            App.Token = login.Content.Token;
            var start = await App.Api.StartSession(new StartSessionRequest(App.Config.StationName));
            if (!start.IsSuccessStatusCode || start.Content == null)
            {
                App.Token = null;
                ShowStatus(ApiErrors.Describe(start), isError: true);
                return;
            }

            App.SessionStartedAt = DateTime.Now;
            App.Session.Start(start.Content);
            _allowClose = true;
            new DashboardWindow(login.Content.Username, start.Content.MinutesLeft).Show();
            Close();
        }
    }
}
