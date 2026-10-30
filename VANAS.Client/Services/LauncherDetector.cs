using System.IO;
using Microsoft.Win32;
using VANAS.Shared.Launchers;

namespace VANAS.Client.Services
{
    public record DetectionResult(
        HashSet<(GameLauncher Launcher, string ExternalId)> Installed,
        bool SteamAvailable,
        bool EpicAvailable,
        string? RiotClientPath,
        Dictionary<(GameLauncher Launcher, string ExternalId), double> Progress,
        string? SteamPath)
    {
        public bool IsInstalled(GameLauncher launcher, string externalId) => Installed.Contains((launcher, externalId));

        public bool LauncherAvailable(GameLauncher launcher) => launcher switch
        {
            GameLauncher.Steam => SteamAvailable,
            GameLauncher.Epic => EpicAvailable,
            _ => RiotClientPath != null
        };
    }

    /// <summary>Finds which launchers and games are present on this PC by reading their own manifests (read-only).</summary>
    public static class LauncherDetector
    {
        public static DetectionResult Detect()
        {
            var installed = new HashSet<(GameLauncher, string)>();
            var progress = new Dictionary<(GameLauncher, string), double>();
            var steamAvailable = DetectSteam(installed, progress);
            var epicAvailable = DetectEpic(installed);
            var riotClient = DetectRiot(installed);
            return new DetectionResult(installed, steamAvailable, epicAvailable, riotClient, progress, SteamPath());
        }

        private static string? SteamPath()
        {
            var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                       ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            return path == null ? null : Path.GetFullPath(path);
        }

        private static bool DetectSteam(HashSet<(GameLauncher, string)> installed, Dictionary<(GameLauncher, string), double> progress)
        {
            var steam = SteamPath();
            var protocolRegistered = Registry.ClassesRoot.OpenSubKey("steam") != null;
            if (steam == null || !Directory.Exists(steam)) return protocolRegistered;

            var libraries = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (TryRead(vdf, out var vdfText))
                libraries.AddRange(InstalledGameParsers.ParseSteamLibraries(vdfText).Select(l => l.Path));

            foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var apps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(apps)) continue;

                foreach (var file in SafeFiles(apps, "appmanifest_*.acf"))
                {
                    if (!TryRead(file, out var acf)) continue;
                    if (InstalledGameParsers.ParseSteamAppManifest(acf) is { } game)
                        installed.Add((GameLauncher.Steam, game.ExternalId));
                    else if (InstalledGameParsers.ParseSteamProgress(acf, out var inProgressId) is { } percent && inProgressId != null)
                        progress[(GameLauncher.Steam, inProgressId)] = percent;
                }
            }
            return true;
        }

        private static bool DetectEpic(HashSet<(GameLauncher, string)> installed)
        {
            var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");

            foreach (var file in SafeFiles(manifests, "*.item"))
            {
                if (TryRead(file, out var json) && InstalledGameParsers.ParseEpicManifest(json) is { } game)
                    installed.Add((GameLauncher.Epic, game.ExternalId));
            }
            return Registry.ClassesRoot.OpenSubKey("com.epicgames.launcher") != null;
        }

        private static string? DetectRiot(HashSet<(GameLauncher, string)> installed)
        {
            var riotData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games");
            var installs = Path.Combine(riotData, "RiotClientInstalls.json");
            if (!TryRead(installs, out var json)) return null;

            var client = FindRiotClient(json);
            var folders = InstalledGameParsers.ParseRiotInstallFolders(json).Where(Directory.Exists).ToList();

            foreach (var (product, hint) in InstalledGameParsers.RiotFolderHints)
            {
                var metadata = Path.Combine(riotData, "Metadata", product + ".live");
                if (Directory.Exists(metadata) && folders.Any(f => f.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                    installed.Add((GameLauncher.Riot, product));
            }
            return client;
        }

        private static string? FindRiotClient(string json)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                foreach (var key in new[] { "rc_default", "rc_live" })
                {
                    if (doc.RootElement.TryGetProperty(key, out var value) && value.GetString() is { } path && File.Exists(path))
                        return Path.GetFullPath(path);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // unreadable: treat Riot as absent
            }
            return null;
        }

        private static IEnumerable<string> SafeFiles(string directory, string pattern)
        {
            try { return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : Array.Empty<string>(); }
            catch (Exception) { return Array.Empty<string>(); }
        }

        private static bool TryRead(string path, out string text)
        {
            text = string.Empty;
            try
            {
                if (!File.Exists(path)) return false;
                // Launchers keep these files open while running: share read access.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
