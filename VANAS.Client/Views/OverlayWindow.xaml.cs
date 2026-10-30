using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace VANAS.Client.Views
{
    /// <summary>
    /// Small always-on-top clock shown over games. It never takes focus and lets clicks go through.
    /// Exclusive-fullscreen games may hide it; borderless/windowed ones will show it.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

        private readonly DispatcherTimer _hideWarning = new() { Interval = TimeSpan.FromSeconds(10) };

        public OverlayWindow()
        {
            InitializeComponent();
            Top = SystemParameters.WorkArea.Top + 6;
            _hideWarning.Tick += (_, _) =>
            {
                _hideWarning.Stop();
                lblWarning.Visibility = Visibility.Collapsed;
            };
        }

        public void SetTime(string text) => lblTime.Text = text;

        public void ShowWarning(string text)
        {
            lblWarning.Text = text;
            lblWarning.Visibility = Visibility.Visible;
            _hideWarning.Stop();
            _hideWarning.Start();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) =>
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
