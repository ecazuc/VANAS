namespace VANAS.Shared.Launchers
{
    public enum GameLauncher
    {
        Steam = 0,
        Epic = 1,
        Riot = 2
    }

    /// <summary>Protocol URIs understood by each launcher.</summary>
    public static class LauncherUris
    {
        public static string? Launch(GameLauncher launcher, string externalId) => launcher switch
        {
            GameLauncher.Steam => $"steam://rungameid/{externalId}",
            GameLauncher.Epic => $"com.epicgames.launcher://apps/{Uri.EscapeDataString(externalId)}?action=launch&silent=true",
            _ => null // Riot is started through its client executable with arguments
        };

        public static string? Install(GameLauncher launcher, string externalId) => launcher switch
        {
            GameLauncher.Steam => $"steam://install/{externalId}",
            GameLauncher.Epic => $"com.epicgames.launcher://apps/{Uri.EscapeDataString(externalId)}?action=install",
            _ => null
        };

        /// <summary>Arguments for RiotClientServices.exe, used for both launching and installing a Riot product.</summary>
        public static string RiotArguments(string product) => $"--launch-product={product} --launch-patchline=live";
    }
}
