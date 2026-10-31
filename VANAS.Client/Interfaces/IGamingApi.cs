using Refit;
using VANAS.Shared.Contracts;

namespace VANAS.Client.Interfaces
{
    public interface IGamingApi
    {
        [Post("/api/auth/login")]
        Task<ApiResponse<LoginResponse>> Login([Body] LoginRequest request);

        [Post("/api/auth/register")]
        Task<IApiResponse<RegisterResult>> Register([Body] RegisterRequest request);

        [Post("/api/sessions/start")]
        Task<ApiResponse<SessionInfo>> StartSession([Body] StartSessionRequest request);

        [Post("/api/sessions/{id}/heartbeat")]
        Task<ApiResponse<HeartbeatResponse>> Heartbeat(int id);

        [Post("/api/sessions/{id}/end")]
        Task<IApiResponse> EndSession(int id);

        [Get("/api/catalog")]
        Task<ApiResponse<List<CatalogGameDto>>> GetCatalog();

        [Get("/api/profile/me")]
        Task<ApiResponse<ProfileInfo>> GetProfile();

        [Put("/api/profile/me")]
        Task<ApiResponse<ProfileInfo>> UpdateProfile([Body] UpdateProfileRequest request);

        [Get("/api/profile/me/sessions")]
        Task<ApiResponse<List<SessionHistoryItem>>> GetSessionHistory([Query] int take = 20);
    }
}
