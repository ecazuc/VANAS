using System.Windows;

namespace VANAS.Client.Services
{
    /// <summary>
    /// Kiosk behaviours (always-on-top lock screen, key blocking, process cleanup).
    /// Only active when client.json enables it AND we run under the dedicated Windows account,
    /// so a developer or staff member can never lock their own session by mistake.
    /// </summary>
    public static class Kiosk
    {
        public static bool Enabled { get; private set; }

        public static void Initialize(ClientConfig config)
        {
            Enabled = config.KioskMode &&
                      string.Equals(Environment.UserName, config.KioskAccount, StringComparison.OrdinalIgnoreCase);
        }

        public static void Lock(Window window)
        {
            if (!Enabled) return;
            window.Topmost = true;
            KeyboardLock.SetMode(KeyboardLockMode.Locked);
            window.Activate();
        }

        public static void EnterSession(Window window)
        {
            if (!Enabled) return;
            window.Topmost = false;
            KeyboardLock.OwnWindowIsForeground = () => window.IsActive;
            KeyboardLock.SetMode(KeyboardLockMode.Session);
        }

        public static void Release() => KeyboardLock.SetMode(KeyboardLockMode.Off);
    }
}
