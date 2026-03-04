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
using VANAS.Shared.Models;

namespace VANAS.Client
{
    /// <summary>
    /// Logique d'interaction pour AdminPanel.xaml
    /// </summary>
    public partial class AdminPanel : Window
    {
        private readonly IGamingApi _api;
        private List<User> _allUsers = new List<User>();

        public AdminPanel(IGamingApi api)
        {
            InitializeComponent();
            _api = api;
            LoadUsers();
        }

        private async void LoadUsers()
        {
            try
            {
                var users = await _api.GetAllUsers();

                // Apply your sorting: Admin first, then Alphabetical
                _allUsers = users
                    .OrderByDescending(u => u.IsAdmin)
                    .ThenBy(u => u.Username)
                    .ToList();

                // Initially show all users
                dgUsers.ItemsSource = _allUsers;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading users: " + ex.Message);
            }
        }

        private async void AddHour_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var user = button?.DataContext as User; // The DataGrid row's data

            if (user != null)
            {
                var request = new AddTimeRequest { Username = user.Username, MinutesToAdd = 60 };
                await _api.AddTime(request);

                MessageBox.Show($"Added 60 minutes to {user.Username}!");
                LoadUsers(); // Refresh the list
            }
        }

        private async void btnCreateUser_Click(object sender, RoutedEventArgs e)
        {
            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(txtNewUser.Text) || string.IsNullOrWhiteSpace(txtNewPass.Password))
            {
                MessageBox.Show("Please enter both a username and password.");
                return;
            }

            if (!double.TryParse(txtNewBalance.Text, out double initialMins))
            {
                initialMins = 60; // Default to 1 hour if input is weird
            }

            // 2. Prepare the Request
            var request = new RegisterRequest
            {
                Username = txtNewUser.Text,
                Password = txtNewPass.Password,
                BalanceMinutes = initialMins
            };

            try
            {
                // 3. Call the Server
                var response = await _api.Register(request);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"User '{request.Username}' created successfully!");

                    // 4. Clear the form and Refresh the list
                    txtNewUser.Clear();
                    txtNewPass.Clear();
                    txtNewBalance.Text = "60";
                    LoadUsers();
                }
                else
                {
                    MessageBox.Show("Error: User might already exist.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection Error: " + ex.Message);
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as Button)?.DataContext as User;
            if (user == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete {user.Username}?", "Confirm", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                await _api.DeleteUser(user.Username);
                LoadUsers(); // Refresh the list
            }
        }

        private async void KickUser_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as Button)?.DataContext as User;
            if (user == null) return;

            await _api.KickUser(user.Username);
            MessageBox.Show($"{user.Username} has been flagged for kick.");
            LoadUsers();
        }

        private async void ToggleAdmin_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as Button)?.DataContext as User;
            if (user == null) return;

            // Flip the status
            var request = new SetAdminRequest
            {
                Username = user.Username,
                IsAdmin = !user.IsAdmin
            };

            try
            {
                var response = await _api.SetAdmin(request);
                if (response.IsSuccessStatusCode)
                {
                    LoadUsers(); // Refresh to update colors and sorting
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating admin status: " + ex.Message);
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgUsers.ItemsSource = _allUsers;
            }
            else
            {
                // Filter the master list and update the DataGrid
                var filteredList = _allUsers
                    .Where(u => u.Username.ToLower().Contains(searchText))
                    .ToList();

                dgUsers.ItemsSource = filteredList;
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            AuthWindow loginWindow = new AuthWindow();
            loginWindow.Show();
            this.Close(); // Close Admin Panel
        }
    }
}
