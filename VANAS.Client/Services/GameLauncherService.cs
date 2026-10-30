using System.Diagnostics;
using System.Text.RegularExpressions;
using VANAS.Shared.Contracts;
using VANAS.Shared.Launchers;

namespace VANAS.Client.Services
{
    /// <summary>Starts and installs games through each launcher protocol. Never builds a command from unvalidated text.</summary>
    public static class GameLauncherService
    {
        private static readonly Regex SafeId = new(@"^[A-Za-z0-9_.\-]{1,100}$", RegexOptions.Compiled);

        public static bool IsSafe(CatalogGameDto game) => SafeId.IsMatch(game.ExternalId);

        /// <summary>Returns null on success, or a message for the customer.</summary>
        public static string? Launch(CatalogGameDto game, DetectionResult detection) => Run(game, detection, install: false);

        public static string? Install(CatalogGameDto game, DetectionResult detection) => Run(game, detection, install: true);

        private static string? Run(CatalogGameDto game, DetectionResult detection, bool install)
        {
            if (!IsSafe(game)) return "Ce jeu est mal configuré. Prévenez le staff.";
            if (!detection.LauncherAvailable(game.Launcher))
                return $"{LauncherName(game.Launcher)} n'est pas installé sur ce poste. Prévenez le staff.";

            try
            {
                if (game.Launcher == GameLauncher.Riot)
                {
                    var info = new ProcessStartInfo(detection.RiotClientPath!) { UseShellExecute = false };
                    info.ArgumentList.Add($"--launch-product={game.ExternalId}");
                    info.ArgumentList.Add("--launch-patchline=live");
                    Process.Start(info);
                }
                else
                {
                    var uri = install ? LauncherUris.Install(game.Launcher, game.ExternalId) : LauncherUris.Launch(game.Launcher, game.ExternalId);
                    Process.Start(new ProcessStartInfo(uri!) { UseShellExecute = true });
                }
                return null;
            }
            catch (Exception ex)
            {
                return $"Impossible de démarrer {game.Title} : {ex.Message}";
            }
        }

        public static string LauncherName(GameLauncher launcher) => launcher switch
        {
            GameLauncher.Steam => "Steam",
            GameLauncher.Epic => "Epic Games",
            _ => "Riot Games"
        };
    }
}
