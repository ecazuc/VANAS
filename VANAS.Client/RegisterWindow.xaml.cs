using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VANAS.Client.Interfaces;

namespace VANAS.Client
{
    public partial class RegisterWindow : Window
    {
        private readonly IGamingApi _api;

        public RegisterWindow(IGamingApi api)
        {
            InitializeComponent();
            _api = api;
        }

        private async void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validation
            if (txtPass.Password != txtConfirmPass.Password)
            {
                MessageBox.Show("Passwords do not match!");
                return;
            }

            var request = new RegisterRequest
            {
                Username = txtUser.Text,
                Password = txtPass.Password,
                BalanceMinutes = 0 // New users start with 0 minutes
            };

            try
            {
                var response = await _api.Register(request);
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Registration successful! You can now log in.");
                    GoToLogin();
                }
                else
                {
                    MessageBox.Show("Registration failed. Username might be taken.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void BackToLogin_Click(object sender, MouseButtonEventArgs e) => GoToLogin();

        private void GoToLogin()
        {
            // Find the hidden MainWindow and show it again
            Application.Current.MainWindow.Show();
            this.Close();
        }
    }
}
