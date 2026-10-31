namespace VANAS.Shared.Models
{
    /// <summary>A tournament or evening organised at the café.</summary>
    public class GameEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string DescriptionMarkdown { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
        public int Capacity { get; set; }
        public int PriceCents { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A customer's seat at an event. The ticket code is what the QR code carries.</summary>
    public class EventRegistration
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CheckedInAt { get; set; }
    }
}
