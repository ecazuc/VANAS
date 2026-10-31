namespace VANAS.Shared.Models
{
    public class Station
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        /// <summary>A staff message waiting for the player on this PC; delivered with the next heartbeat, then cleared.</summary>
        public string? PendingMessage { get; set; }

        public DateTime? PendingMessageAt { get; set; }
    }
}
