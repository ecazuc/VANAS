namespace VANAS.Shared.Models
{
    /// <summary>A blog post (news about games, café events, guides). Content is Markdown, rendered on the website.</summary>
    public class Article
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string ContentMarkdown { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>Comma-separated tags (lowercase).</summary>
        public string Tags { get; set; } = string.Empty;

        public string? CoverUrl { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? AuthorId { get; set; }
    }
}
