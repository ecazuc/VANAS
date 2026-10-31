using System.Windows;
using VANAS.Client.Services;
using VANAS.Shared.Security;

namespace VANAS.Client.Views
{
    public partial class PinWindow : Window
    {
        private const int MaxAttempts = 3;
        private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(1);

        private static int _failures;
        private static DateTime _lockedUntil = DateTime.MinValue;

        public PinWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => { Activate(); txtPin.Focus(); AppLog.Info("Admin PIN window opened."); };
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (DateTime.UtcNow < _lockedUntil)
            {
                lblError.Text = $"Trop d'essais. Réessayez dans {(int)(_lockedUntil - DateTime.UtcNow).TotalSeconds} s.";
                return;
            }

            if (PinHasher.Verify(txtPin.Password, App.Config.AdminPinHash))
            {
                _failures = 0;
                AppLog.Warn("Admin PIN accepted: leaving kiosk mode.");
                DialogResult = true;
                return;
            }

            txtPin.Clear();
            AppLog.Warn($"Admin PIN refused (attempt {_failures + 1}).");
            if (++_failures >= MaxAttempts)
            {
                _failures = 0;
                _lockedUntil = DateTime.UtcNow + Lockout;
                lblError.Text = "Trop d'essais. Verrouillé pendant 1 minute.";
            }
            else
            {
                lblError.Text = "Code incorrect.";
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
