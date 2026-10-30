using VANAS.Shared.Launchers;

namespace VANAS.Shared.Models
{
    /// <summary>A game the café allows on its PCs. Managed by staff; only enabled games are shown to customers.</summary>
    public class CatalogGame
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public GameLauncher Launcher { get; set; }

        /// <summary>Steam appid, Epic AppName, or Riot product id (valorant, league_of_legends...).</summary>
        public string ExternalId { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
