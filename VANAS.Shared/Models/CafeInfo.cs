namespace VANAS.Shared.Models
{
    /// <summary>The practical information shown on the website's "Le café" page. One row, edited by an admin in the staff console.</summary>
    public class CafeInfo
    {
        public int Id { get; set; } = 1;

        /// <summary>Opening hours, one line per day or group of days (plain text).</summary>
        public string OpeningHours { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>Free text in Markdown: equipment of the PCs, house rules, how to get there...</summary>
        public string AboutMarkdown { get; set; } = string.Empty;

        /// <summary>Photo addresses (http/https), one per line.</summary>
        public string PhotoUrls { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
