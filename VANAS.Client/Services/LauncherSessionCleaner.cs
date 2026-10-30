using Microsoft.Win32;
using VANAS.Shared.Launchers;

namespace VANAS.Client.Services
{
    /// <summary>
    /// End-of-session hygiene: forget the logged-in Steam / Epic / Riot account so the next customer starts clean.
    /// Only runs in kiosk mode (dedicated Windows account), after the launchers have been closed.
    /// </summary>
    public static class LauncherSessionCleaner
    {
        public static void Wipe()
        {
            if (!Kiosk.Enabled || !App.Config.WipeLauncherLogins) return;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;

            LauncherLoginCleanup.Wipe(
                LauncherLoginCleanup.FilesToDelete(steamPath, localAppData),
                LauncherLoginCleanup.DirectoriesToDelete(localAppData));

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", writable: true);
                key?.SetValue("AutoLoginUser", string.Empty);
                key?.SetValue("RememberPassword", 0, RegistryValueKind.DWord);
            }
            catch (Exception)
            {
                // Steam key missing or locked: nothing more to forget.
            }
        }
    }
}
