using Refit;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VANAS.Shared.Models;


namespace VANAS.Client.Interfaces
{
    public interface IGamingApi
    {
        [Post("/api/auth/login")]
        Task<ApiResponse<LoginResponse>> Login([Body] LoginRequest request);

        [Get("/api/auth/users")]
        Task<List<User>> GetAllUsers();

        [Post("/api/auth/add-time")]
        Task<ApiResponse<object>> AddTime([Body] AddTimeRequest request);

        [Post("/api/auth/register")]
        Task<ApiResponse<object>> Register([Body] RegisterRequest request);

        [Delete("/api/auth/users/{username}")]
        Task<ApiResponse<object>> DeleteUser(string username);

        [Post("/api/auth/kick/{username}")]
        Task<ApiResponse<object>> KickUser(string username);

        [Put("/api/auth/update-balance")]
        Task<ApiResponse<object>> UpdateBalance([Body] AddTimeRequest request);

        [Put("/api/auth/set-admin")]
        Task<ApiResponse<object>> SetAdmin([Body] SetAdminRequest request);
    }

    // These match the data shapes from your Server
    public class LoginRequest { public required string Username { get; set; } public required string Password { get; set; } }
    

public class LoginResponse
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("minutesLeft")]
        public double MinutesLeft { get; set; }

        [JsonPropertyName("isAdmin")] // <--- This forces it to match the Server's JSON
        public bool IsAdmin { get; set; }
    }
    public class AddTimeRequest { public required string Username { get; set; } public double MinutesToAdd { get; set; } }
    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public double BalanceMinutes { get; set; }
    }

    public class SetAdminRequest
    {
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }


}
