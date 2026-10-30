namespace VANAS.Shared.Models
{
    public class Profile
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}
