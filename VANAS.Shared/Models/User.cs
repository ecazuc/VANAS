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

        public Profile? Profile { get; set; }
    }
}
