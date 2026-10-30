using System.Text.Json;

namespace VANAS.Shared.Launchers
{
    public record SteamLibrary(string Path);
    public record InstalledGame(GameLauncher Launcher, string ExternalId, string Name);

    /// <summary>Pure parsing of launcher manifests, so it can be unit-tested without Steam/Epic installed.</summary>
    public static class InstalledGameParsers
    {
        /// <summary>Folders listed in Steam libraryfolders.vdf (each one holds a steamapps directory).</summary>
        public static List<SteamLibrary> ParseSteamLibraries(string vdf)
        {
            var result = new List<SteamLibrary>();
            var root = ValveKeyValues.Parse(vdf);
            var folders = ValveKeyValues.GetObject(root, "libraryfolders");
            if (folders == null) return result;

            foreach (var entry in folders.Values.OfType<Dictionary<string, object>>())
            {
                var path = ValveKeyValues.GetString(entry, "path");
                if (!string.IsNullOrWhiteSpace(path)) result.Add(new SteamLibrary(path));
            }
            return result;
        }

        /// <summary>An appmanifest_*.acf. Returns null unless the game is fully installed (StateFlags bit 4).</summary>
        public static InstalledGame? ParseSteamAppManifest(string acf)
        {
            var state = ValveKeyValues.GetObject(ValveKeyValues.Parse(acf), "AppState");
            if (state == null) return null;

            var appId = ValveKeyValues.GetString(state, "appid");
            var name = ValveKeyValues.GetString(state, "name");
            if (appId == null || name == null) return null;
            if (!long.TryParse(ValveKeyValues.GetString(state, "StateFlags"), out var flags) || (flags & 4) == 0) return null;

            return new InstalledGame(GameLauncher.Steam, appId, name);
        }

        /// <summary>An Epic Games Launcher .item manifest. Returns null for incomplete installs or unreadable files.</summary>
        public static InstalledGame? ParseEpicManifest(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return null;

                var appName = root.TryGetProperty("AppName", out var a) ? a.GetString() : null;
                var display = root.TryGetProperty("DisplayName", out var d) ? d.GetString() : null;
                return appName == null ? null : new InstalledGame(GameLauncher.Epic, appName, display ?? appName);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Install folders listed under "associated_client" in RiotClientInstalls.json.</summary>
        public static List<string> ParseRiotInstallFolders(string json)
        {
            var folders = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("associated_client", out var assoc) && assoc.ValueKind == JsonValueKind.Object)
                    folders.AddRange(assoc.EnumerateObject().Select(p => p.Name));
            }
            catch (JsonException)
            {
                // unreadable file: no Riot games detected
            }
            return folders;
        }

        /// <summary>Riot product id to the folder name its install directory contains.</summary>
        public static readonly IReadOnlyDictionary<string, string> RiotFolderHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["valorant"] = "VALORANT",
            ["league_of_legends"] = "League of Legends",
            ["teamfighttactics"] = "League of Legends",
            ["bacon"] = "Legends of Runeterra",
            ["lion"] = "2XKO"
        };
    }
}
