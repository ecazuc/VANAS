using System.IO;
using System.Text;

namespace VANAS.Client.Services
{
    /// <summary>
    /// A small daily log file, kept 14 days, to understand what happened on a PC after the fact.
    /// Located in C:\ProgramData\VANAS\logs (or the user's local app data when that is not writable).
    /// It never records passwords, tokens or player names.
    /// </summary>
    public static class AppLog
    {
        private const int KeepDays = 14;
        private static readonly object Gate = new();
        private static string? _folder;

        public static string? Folder => _folder;

        public static void Init(string version, string station)
        {
            _folder = FindWritableFolder();
            if (_folder == null) return;

            try
            {
                foreach (var old in Directory.GetFiles(_folder, "client-*.log").Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-KeepDays)))
                    File.Delete(old);
            }
            catch (IOException) { /* cleanup is a nicety */ }

            Info($"Client {version} started on station '{station}' (Windows user '{Environment.UserName}').");
        }

        public static void Info(string message) => Write("INFO ", message);

        public static void Warn(string message) => Write("WARN ", message);

        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

        private static void Write(string level, string message)
        {
            if (_folder == null) return;
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
                lock (Gate) File.AppendAllText(Path.Combine(_folder, $"client-{DateTime.Now:yyyy-MM-dd}.log"), line, Encoding.UTF8);
            }
            catch (IOException) { /* a full disk or a locked file must never stop the client */ }
            catch (UnauthorizedAccessException) { }
        }

        private static string? FindWritableFolder()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VANAS", "logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VANAS", "logs")
            };

            foreach (var folder in candidates)
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    var probe = Path.Combine(folder, ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return folder;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }

            return null;
        }
    }
}
