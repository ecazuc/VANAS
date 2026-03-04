using System.Windows;
using System.Windows.Input;
using Refit;
using VANAS.Client.Interfaces;

namespace VANAS.Client
{
    /// <summary>
    /// Logique d'interaction pour AuthWindow.xaml
    /// </summary>
    public partial class AuthWindow : Window
    {
        private readonly IGamingApi _api;

        public AuthWindow()
        {
            InitializeComponent();

            // Tell Refit where your server is running. 
            // Check your browser URL when the server runs (usually http://localhost:5000 or 7000)
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            _api = RestService.For<IGamingApi>("http://localhost:5049");
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            lblStatus.Text = "Connecting...";

            try
            {
                var request = new LoginRequest
                {
                    Username = txtUsername.Text,
                    Password = txtPassword.Password
                };

                var response = await _api.Login(request);

                if (response.IsSuccessStatusCode && response.Content != null)
                {
                    if (response.Content.IsAdmin == true)
                    {
                        var adminWindow = new AdminPanel(_api);
                        adminWindow.Show();
                    }
                    else
                    {
                        var dashboard = new Dashboard(response.Content.Username, response.Content.MinutesLeft);
                        dashboard.Show();
                    }
                    this.Close();

                    // Close the login window
                    this.Close();
                }
                else
                {
                    lblStatus.Text = "Invalid login credentials.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: Could not connect to server :";
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private void linkSignUp_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Open the Register Window and pass the API instance
            RegisterWindow regWindow = new RegisterWindow(_api);
            regWindow.Show();

            // We don't close the Login window yet, just in case they want to go back
            this.Hide();
        }
    }
}
