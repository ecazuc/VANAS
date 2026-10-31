using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;

namespace VANAS.Server.Services
{
    /// <summary>
    /// Deletes old chat data so nothing is kept longer than needed (GDPR).
    /// Messages go after Chat:RetentionDays (default 90). A message that was reported is kept as evidence until
    /// Chat:ReportRetentionDays (default 365) after its report was settled, or for as long as the report is open.
    /// </summary>
    public class ChatRetentionService : BackgroundService
    {
        private static readonly TimeSpan Every = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ChatRetentionService> _log;
        private readonly int _messageDays;
        private readonly int _reportDays;

        public ChatRetentionService(IServiceScopeFactory scopes, IConfiguration config, ILogger<ChatRetentionService> log)
        {
            _scopes = scopes;
            _log = log;
            _messageDays = config.GetValue("Chat:RetentionDays", 90);
            _reportDays = config.GetValue("Chat:ReportRetentionDays", 365);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var deleted = await PurgeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), DateTime.UtcNow, _messageDays, _reportDays);
                    if (deleted > 0) _log.LogInformation("Chat retention: {Count} old message(s) or report(s) deleted.", deleted);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Chat retention failed; it will retry later.");
                }

                await Task.Delay(Every, stoppingToken);
            }
        }

        /// <summary>Returns how many rows were deleted.</summary>
        public static async Task<int> PurgeAsync(AppDbContext db, DateTime now, int messageDays, int reportDays)
        {
            var messageLimit = now.AddDays(-messageDays);
            var reportLimit = now.AddDays(-reportDays);

            // 1. Settled reports older than the evidence period go away.
            var oldReports = await db.MessageReports.Where(r => r.ResolvedAt != null && r.ResolvedAt < reportLimit).ToListAsync();
            db.MessageReports.RemoveRange(oldReports);
            await db.SaveChangesAsync();

            // 2. Messages past the retention period go away, unless a remaining report still needs them.
            var kept = await db.MessageReports.Select(r => r.MessageId).Distinct().ToListAsync();
            var oldMessages = await db.ChatMessages.Where(m => m.CreatedAt < messageLimit && !kept.Contains(m.Id)).ToListAsync();
            db.ChatMessages.RemoveRange(oldMessages);
            await db.SaveChangesAsync();

            return oldReports.Count + oldMessages.Count;
        }
    }
}
