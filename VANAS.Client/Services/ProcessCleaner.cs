using System.Diagnostics;

namespace VANAS.Client.Services
{
    /// <summary>Closes everything the customer started (games, launchers) once their session ends.</summary>
    public static class ProcessCleaner
    {
        public static void KillStartedSince(DateTime since)
        {
            if (!Kiosk.Enabled || !App.Config.CloseProgramsOnLogout) return;

            var self = Environment.ProcessId;
            var sessionId = Process.GetCurrentProcess().SessionId;
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == self || process.SessionId != sessionId || process.StartTime < since) continue;

                    var path = process.MainModule?.FileName;
                    if (path == null || path.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase)) continue;

                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already exited, or protected (elevated / system): nothing more we can do for this one.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }
}
