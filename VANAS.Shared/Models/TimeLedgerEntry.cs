namespace VANAS.Shared.Models
{
    public enum LedgerReason
    {
        Purchase = 0,
        Adjustment = 1
    }

    /// <summary>Credit (or correction) of minutes. Consumption is tracked per <see cref="GameSession"/>.</summary>
    public class TimeLedgerEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public double Minutes { get; set; }
        public LedgerReason Reason { get; set; }
        public string? Note { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
