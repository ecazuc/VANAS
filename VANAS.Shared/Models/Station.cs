namespace VANAS.Shared.Models
{
    public class Station
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
