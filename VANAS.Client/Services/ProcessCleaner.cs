using System.Diagnostics;

namespace VANAS.Client.Services
{
    /// <summary>
    /// Closes everything the customer started (games, launchers) once their session ends, then makes the
    /// launchers forget the logged-in account. Kiosk mode only.
    /// </summary>
    public static class ProcessCleaner
    {
        /// <summary>Launchers are closed even if they were started before the session (e.g. at Windows logon).</summary>
        private static readonly string[] LauncherProcesses =
        {
            "steam", "steamwebhelper", "steamservice",
            "EpicGamesLauncher", "EpicWebHelper", "UnrealCEFSubProcess",
            "RiotClientServices", "RiotClientUx", "RiotClientUxRender", "RiotClientCrashHandler", "Riot Client"
        };

        public static void CleanAfterSession(DateTime sessionStart)
        {
            if (!Kiosk.Enabled || !App.Config.CloseProgramsOnLogout) return;

            var killed = new List<Process>();
            killed.AddRange(KillStartedSince(sessionStart));
            killed.AddRange(KillLaunchers());

            foreach (var process in killed)
            {
                try { process.WaitForExit(3000); } catch { /* already gone */ }
                process.Dispose();
            }

            LauncherSessionCleaner.Wipe();
        }

        private static List<Process> KillStartedSince(DateTime since)
        {
            var killed = new List<Process>();
            var self = Environment.ProcessId;
            var sessionId = Process.GetCurrentProcess().SessionId;
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == self || process.SessionId != sessionId || process.StartTime < since) { process.Dispose(); continue; }

                    var path = process.MainModule?.FileName;
                    if (path == null || path.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase)) { process.Dispose(); continue; }

                    process.Kill(entireProcessTree: true);
                    killed.Add(process);
                }
                catch
                {
                    // Already exited, or protected (elevated / system): nothing more we can do for this one.
                    process.Dispose();
                }
            }
            return killed;
        }

        private static List<Process> KillLaunchers()
        {
            var killed = new List<Process>();
            var self = Environment.ProcessId;
            var sessionId = Process.GetCurrentProcess().SessionId;

            foreach (var name in LauncherProcesses)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (process.Id == self || process.SessionId != sessionId) { process.Dispose(); continue; }
                        process.Kill(entireProcessTree: true);
                        killed.Add(process);
                    }
                    catch
                    {
                        process.Dispose();
                    }
                }
            }
            return killed;
        }
    }
}
