namespace VANAS.Shared.Models
{
    public class UserSession
    {
        public required string Username { get; set; }
        public double RemainingMinutes { get; set; }
        public required string Token { get; set; } // This is the JWT "ticket"
        public DateTime SessionExpiry { get; set; }
    }
}
