namespace VANAS.Shared.Models
{
    /// <summary>A game a player says they play. It gives access to that game's room and suggests players who share it.</summary>
    public class PlayerGame
    {
        public int UserId { get; set; }
        public int GameId { get; set; }
    }

    /// <summary>A chat message: either in a game's room (GameId set) or private (RecipientId set).</summary>
    public class ChatMessage
    {
        public int Id { get; set; }
        public int? GameId { get; set; }
        public int SenderId { get; set; }
        public int? RecipientId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Hidden by staff after a report: no longer shown to anyone.</summary>
        public bool IsHidden { get; set; }
    }

    public class UserBlock
    {
        public int BlockerId { get; set; }
        public int BlockedId { get; set; }
    }

    public class MessageReport
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int ReporterId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public string? Resolution { get; set; }
    }

    /// <summary>Restriction applied by staff to a player's chat (temporary mute or permanent ban).</summary>
    public class ChatSanction
    {
        public int UserId { get; set; }
        public DateTime? MutedUntil { get; set; }
        public bool IsBanned { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ModerationAction
    {
        Dismiss = 0,
        HideMessage = 1,
        Mute24Hours = 2,
        Mute7Days = 3,
        Ban = 4
    }
}
