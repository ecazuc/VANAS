using VANAS.Shared.Models;

namespace VANAS.Shared.Contracts
{
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string Token, string Username, UserRole Role, double MinutesLeft, bool ConsentPending = false);

    public record RegisterRequest(string Username, string Password, DateOnly? BirthDate = null);

    /// <summary>ConsentPending: the player is a minor and a parent must agree at the counter before playing or chatting.</summary>
    public record RegisterResult(bool ConsentPending);

    public record AddTimeRequest(double Minutes, string? Note);

    public record SetRoleRequest(UserRole Role);

    public record UserSummary(int Id, string Username, UserRole Role, double MinutesLeft, bool IsOnline, int? Age = null, bool ConsentPending = false, bool ConsentRecorded = false);

    public record StartSessionRequest(string StationName);

    public record SessionInfo(int SessionId, double MinutesLeft);

    public enum SessionStatus
    {
        Active = 0,
        TimeExpired = 1,
        Kicked = 2
    }

    public record HeartbeatResponse(SessionStatus Status, double MinutesLeft, string? StaffMessage = null);

    public record StationMessageRequest(string Text);
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
        DateTime MemberSince,
        string? PassSummary = null);

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

namespace VANAS.Shared.Contracts
{
    public record EventSummary(int Id, string Title, string GameName, DateTime StartsAt, int Capacity, int RegisteredCount, int PriceCents, bool IsPublished);

    public record EventDetail(
        int Id, string Title, string GameName, string DescriptionMarkdown, DateTime StartsAt,
        int Capacity, int RegisteredCount, int PriceCents, bool IsPublished);

    public record SaveEventRequest(string Title, string GameName, string DescriptionMarkdown, DateTime StartsAt, int Capacity, int PriceCents, bool IsPublished);

    public record TicketInfo(int EventId, string EventTitle, DateTime StartsAt, string TicketCode, bool CheckedIn);

    public record RegistrationInfo(string Username, string DisplayName, string TicketCode, DateTime CreatedAt, DateTime? CheckedInAt);

    public record CheckInRequest(string TicketCode);

    public record CheckInResult(string DisplayName, string EventTitle);
}

namespace VANAS.Shared.Contracts
{
    /// <summary>What the mobile app shows live: the balance and whether the player is currently on a PC.</summary>
    public record LiveStatus(double MinutesLeft, bool InSession, string? StationName);
}

namespace VANAS.Shared.Contracts
{
    public record OfferDto(
        int Id, string Name, string Description, VANAS.Shared.Models.OfferKind Kind, int Minutes, int DurationDays,
        int DailyLimitMinutes, int PriceCents, bool IsActive, int SortOrder);

    public record SaveOfferRequest(
        string Name, string Description, VANAS.Shared.Models.OfferKind Kind, int Minutes, int DurationDays,
        int DailyLimitMinutes, int PriceCents, bool IsActive, int SortOrder);

    public record SellRequest(int UserId, int OfferId, VANAS.Shared.Models.PaymentMethod Method, string? Note);

    public record SaleDto(
        int Id, DateTime CreatedAt, string UserName, string OfferName, VANAS.Shared.Models.OfferKind Kind, int PriceCents,
        VANAS.Shared.Models.PaymentMethod Method, string SoldBy, string? Note);

    public record SellResult(SaleDto Sale, double MinutesLeft);

    public record MethodTotal(VANAS.Shared.Models.PaymentMethod Method, int Count, int TotalCents);

    public record SalesReport(DateTime Day, List<SaleDto> Items, int TotalCents, List<MethodTotal> ByMethod);

    /// <summary>A pass as shown to the player: MinutesLeft is the quota left, or the daily allowance left for an unlimited pass (null when uncapped).</summary>
    public record SubscriptionInfo(
        int Id, string Name, VANAS.Shared.Models.OfferKind Kind, DateTime StartsAt, DateTime EndsAt, bool IsActive, double? MinutesLeft);
}

namespace VANAS.Shared.Contracts
{
    public record ChatStatus(bool RulesAccepted, bool IsBanned, DateTime? MutedUntil, bool ConsentPending = false);

    public record SetGamesRequest(List<int> GameIds);

    public record PlayerSuggestion(int UserId, string DisplayName, string AvatarKey, List<string> SharedGames);

    public record ChatMessageDto(
        int Id, int? GameId, int SenderId, string SenderName, string AvatarKey, int? RecipientId, string Text, DateTime CreatedAt);

    public record SendMessageRequest(string Text);

    public record ConversationDto(int UserId, string DisplayName, string AvatarKey, string LastText, DateTime LastAt);

    public record BlockedPlayer(int UserId, string DisplayName);

    public record ReportRequest(string Reason);

    public record ReportDto(
        int Id, DateTime CreatedAt, int MessageId, string MessageText, bool MessageHidden, int SenderId, string SenderName,
        string ReporterName, string Reason, string Context);

    public record ModerateRequest(VANAS.Shared.Models.ModerationAction Action);

    public record SanctionDto(int UserId, string DisplayName, DateTime? MutedUntil, bool IsBanned, string? Reason);
}
