using VANAS.Shared.Launchers;
using VANAS.Shared.Models;

namespace VANAS.Server.Data
{
    /// <summary>Starter catalog (popular free-to-play games). Staff can edit it from the web console.</summary>
    public static class CatalogSeed
    {
        public static IEnumerable<CatalogGame> Games()
        {
            yield return new CatalogGame { Title = "Counter-Strike 2", Launcher = GameLauncher.Steam, ExternalId = "730" };
            yield return new CatalogGame { Title = "Dota 2", Launcher = GameLauncher.Steam, ExternalId = "570" };
            yield return new CatalogGame { Title = "Apex Legends", Launcher = GameLauncher.Steam, ExternalId = "1172470" };
            yield return new CatalogGame { Title = "Team Fortress 2", Launcher = GameLauncher.Steam, ExternalId = "440" };
            yield return new CatalogGame { Title = "Warframe", Launcher = GameLauncher.Steam, ExternalId = "230410" };
            yield return new CatalogGame { Title = "PUBG: Battlegrounds", Launcher = GameLauncher.Steam, ExternalId = "578080" };
            yield return new CatalogGame { Title = "Fortnite", Launcher = GameLauncher.Epic, ExternalId = "Fortnite" };
            yield return new CatalogGame { Title = "Rocket League", Launcher = GameLauncher.Epic, ExternalId = "Sugar" };
            yield return new CatalogGame { Title = "VALORANT", Launcher = GameLauncher.Riot, ExternalId = "valorant" };
            yield return new CatalogGame { Title = "League of Legends", Launcher = GameLauncher.Riot, ExternalId = "league_of_legends" };
            yield return new CatalogGame { Title = "Teamfight Tactics", Launcher = GameLauncher.Riot, ExternalId = "teamfighttactics" };
            yield return new CatalogGame { Title = "Legends of Runeterra", Launcher = GameLauncher.Riot, ExternalId = "bacon" };
        }
    }
}
