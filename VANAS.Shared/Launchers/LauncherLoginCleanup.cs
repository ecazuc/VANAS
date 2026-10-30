namespace VANAS.Shared.Launchers
{
    /// <summary>
    /// Where each launcher remembers who is logged in. Wiping these at the end of a session stops the next
    /// customer from landing in the previous customer's Steam / Epic / Riot account.
    /// Only ever run under the dedicated kiosk Windows account.
    /// </summary>
    public static class LauncherLoginCleanup
    {
        public static List<string> FilesToDelete(string? steamPath, string localAppData)
        {
            var files = new List<string>
            {
                Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Config", "Windows", "GameUserSettings.ini"),
                Path.Combine(localAppData, "Riot Games", "Riot Client", "Data", "RiotGamesPrivateSettings.yaml")
            };
            if (!string.IsNullOrEmpty(steamPath))
                files.Add(Path.Combine(steamPath, "config", "loginusers.vdf"));
            return files;
        }

        public static List<string> DirectoriesToDelete(string localAppData)
        {
            var dirs = new List<string> { Path.Combine(localAppData, "Riot Games", "Riot Client", "Data", "Sessions") };

            var epicSaved = Path.Combine(localAppData, "EpicGamesLauncher", "Saved");
            try
            {
                if (Directory.Exists(epicSaved))
                    dirs.AddRange(Directory.GetDirectories(epicSaved, "webcache*"));
            }
            catch (IOException) { /* nothing to enumerate */ }
            catch (UnauthorizedAccessException) { /* cannot enumerate */ }

            return dirs;
        }

        /// <summary>Deletes what exists, ignoring files that are locked or missing. Returns how many items were removed.</summary>
        public static int Wipe(IEnumerable<string> files, IEnumerable<string> directories)
        {
            var removed = 0;
            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    File.Delete(file);
                    removed++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            foreach (var dir in directories)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return removed;
        }
    }
}
