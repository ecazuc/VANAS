namespace VANAS.Shared.Models
{
    public enum OfferKind
    {
        /// <summary>Hours added to the account balance. They never expire.</summary>
        HourPack = 0,

        /// <summary>Unlimited play during the pass, optionally capped per day.</summary>
        UnlimitedPass = 1,

        /// <summary>A quota of minutes to use during the pass; what is left is lost at the end.</summary>
        QuotaPass = 2,

        /// <summary>An event entry paid at the door. Never sold as an offer: it only appears on sales.</summary>
        EventTicket = 3
    }

    public enum PaymentMethod
    {
        Cash = 0,
        Card = 1,
        Other = 2
    }

    /// <summary>Something the café sells. Prices are set by an admin in the staff console.</summary>
    public class Offer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public OfferKind Kind { get; set; }

        /// <summary>HourPack: minutes credited. QuotaPass: minutes of the quota. Unused for an unlimited pass.</summary>
        public int Minutes { get; set; }

        /// <summary>Validity of a pass, in days (unused for an hour pack).</summary>
        public int DurationDays { get; set; }

        /// <summary>Unlimited pass only: maximum minutes per day, 0 for no daily limit.</summary>
        public int DailyLimitMinutes { get; set; }

        public int PriceCents { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    /// <summary>One counter sale. Names and price are copied so the record stays correct if the offer or the account changes.</summary>
    public class Sale
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int? OfferId { get; set; }
        public string OfferName { get; set; } = string.Empty;
        public OfferKind OfferKind { get; set; }
        public int PriceCents { get; set; }
        public PaymentMethod Method { get; set; }
        public int SoldByUserId { get; set; }
        public string SoldByName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Set when staff cancel the sale (refund): it stays in the books but no longer counts in the totals.</summary>
        public DateTime? CancelledAt { get; set; }

        public string? CancelledByName { get; set; }
        public string? CancelReason { get; set; }
    }

    /// <summary>A pass owned by a player (created by selling a pass offer).</summary>
    public class Subscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SaleId { get; set; }
        public string OfferName { get; set; } = string.Empty;
        public OfferKind Kind { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public int QuotaMinutes { get; set; }
        public int DailyLimitMinutes { get; set; }
    }

    /// <summary>Minutes of play charged to a pass instead of the account balance.</summary>
    public class PassUsage
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public double Minutes { get; set; }
        public DateTime At { get; set; } = DateTime.UtcNow;
    }
}
