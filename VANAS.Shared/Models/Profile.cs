namespace VANAS.Shared.Models
{
    public class Profile
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Key of one of the preset avatars (see <c>VANAS.Shared.Contracts.Avatars</c>).</summary>
        public string AvatarKey { get; set; } = "neon-yellow";

        /// <summary>When the player accepted the chat rules; null until then (no chat before).</summary>
        public DateTime? ChatRulesAcceptedAt { get; set; }
    }
}
