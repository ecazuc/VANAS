using VANAS.Shared.Models;

namespace VANAS.Shared.Contracts
{
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string Token, string Username, UserRole Role, double MinutesLeft);

    public record RegisterRequest(string Username, string Password);

    public record AddTimeRequest(double Minutes, string? Note);

    public record SetRoleRequest(UserRole Role);

    public record UserSummary(int Id, string Username, UserRole Role, double MinutesLeft, bool IsOnline);

    public record StartSessionRequest(string StationName);

    public record SessionInfo(int SessionId, double MinutesLeft);

    public enum SessionStatus
    {
        Active = 0,
        TimeExpired = 1,
        Kicked = 2
    }

    public record HeartbeatResponse(SessionStatus Status, double MinutesLeft);
}

namespace VANAS.Shared.Contracts
{
    /// <summary>One PC of the café with its current session, if any.</summary>
    public record StationStatus(
        int StationId,
        string Name,
        string? IpAddress,
        DateTime LastSeenAt,
        bool IsInUse,
        int? UserId,
        string? Username,
        double? MinutesLeft,
        DateTime? SessionStartedAt);

    public record CreateUserRequest(string Username, string Password, double InitialMinutes);
}

namespace VANAS.Shared.Contracts
{
    public record ProfileInfo(
        string Username,
        string DisplayName,
        string AvatarKey,
        double MinutesLeft,
        double TotalMinutesPlayed,
        int SessionCount,
        DateTime MemberSince);

    public record UpdateProfileRequest(string DisplayName, string AvatarKey);

    public record SessionHistoryItem(
        DateTime StartedAt,
        DateTime? EndedAt,
        double MinutesConsumed,
        string StationName,
        VANAS.Shared.Models.SessionEndReason? EndReason);
}

namespace VANAS.Shared.Contracts
{
    public record CatalogGameDto(int Id, string Title, VANAS.Shared.Launchers.GameLauncher Launcher, string ExternalId, bool IsEnabled);

    public record SaveCatalogGameRequest(string Title, VANAS.Shared.Launchers.GameLauncher Launcher, string ExternalId, bool IsEnabled);
}

namespace VANAS.Shared.Contracts
{
    public record ArticleSummary(int Id, string Title, string Slug, string Summary, string Category, string Tags, string? CoverUrl, bool IsPublished, DateTime? PublishedAt);

    public record ArticleDetail(
        int Id, string Title, string Slug, string Summary, string ContentMarkdown, string Category, string Tags,
        string? CoverUrl, bool IsPublished, DateTime? PublishedAt, DateTime UpdatedAt);

    public record ArticlePage(List<ArticleSummary> Items, int Page, int PageSize, int Total);

    public record SaveArticleRequest(string Title, string Summary, string ContentMarkdown, string Category, string Tags, string? CoverUrl, bool IsPublished);
}
