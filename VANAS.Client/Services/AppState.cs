using System.IO;
using System.Text.Json;

namespace VANAS.Client.Services
{
    /// <summary>
    /// Tells the automatic updater whether a customer is playing. The client is the Windows shell on a kiosk PC, so it is
    /// always running: the updater needs to know when it is safe to stop it. The file is refreshed every 30 seconds and at
    /// every start and end of a session; an old file means "do not trust it" (the updater then waits).
    /// </summary>
    public static class AppState
    {
        private static string? _path;

        public static void Init()
        {
            if (AppLog.Folder == null) return;
            // next to the logs folder: C:\ProgramData\VANAS\state.json
            _path = Path.GetFullPath(Path.Combine(AppLog.Folder, "..", "state.json"));
        }

        public static void Write(bool inSession)
        {
            if (_path == null) return;
            try
            {
                var json = JsonSerializer.Serialize(new { inSession, at = DateTime.UtcNow, version = App.Version });
                File.WriteAllText(_path, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the updater simply waits when it cannot read a fresh state
            }
        }
    }
}
