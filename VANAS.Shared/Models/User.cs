namespace VANAS.Shared.Models
{
    public enum UserRole
    {
        Client = 0,
        Staff = 1,
        Admin = 2
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Client;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Required at sign-up. Null only for accounts created before ages were collected (and the seeded admin): treated as adults.</summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>When staff recorded a parent's agreement at the counter; null until then.</summary>
        public DateTime? ParentalConsentAt { get; set; }

        public int? ParentalConsentById { get; set; }

        public Profile? Profile { get; set; }
    }
}
