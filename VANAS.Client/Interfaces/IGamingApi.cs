using Refit;
using VANAS.Shared.Contracts;

namespace VANAS.Client.Interfaces
{
    public interface IGamingApi
    {
        [Post("/api/auth/login")]
        Task<ApiResponse<LoginResponse>> Login([Body] LoginRequest request);

        [Post("/api/auth/register")]
        Task<IApiResponse> Register([Body] RegisterRequest request);

        [Post("/api/sessions/start")]
        Task<ApiResponse<SessionInfo>> StartSession([Body] StartSessionRequest request);

        [Post("/api/sessions/{id}/heartbeat")]
        Task<ApiResponse<HeartbeatResponse>> Heartbeat(int id);

        [Post("/api/sessions/{id}/end")]
        Task<IApiResponse> EndSession(int id);
    }
}
