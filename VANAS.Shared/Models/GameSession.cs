namespace VANAS.Shared.Models
{
    public enum SessionEndReason
    {
        UserLogout = 0,
        TimeExpired = 1,
        Kicked = 2,
        Replaced = 3
    }

    public class GameSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int StationId { get; set; }
        public Station? Station { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public SessionEndReason? EndReason { get; set; }
        public double MinutesConsumed { get; set; }
        public bool KickRequested { get; set; }
    }
}
