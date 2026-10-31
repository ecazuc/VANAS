using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Models;

namespace VANAS.Server.Services
{
    /// <summary>A pass that is valid now, with the minutes it can still cover.</summary>
    public record ActivePass(Subscription Subscription, double AllowedMinutes);

    public class BalanceService
    {
        private readonly AppDbContext _db;

        public BalanceService(AppDbContext db) => _db = db;

        /// <summary>Hours bought by the player: credits minus time consumed in sessions (never expires).</summary>
        public async Task<double> GetPackMinutesAsync(int userId)
        {
            var credits = await _db.TimeLedger.Where(t => t.UserId == userId).SumAsync(t => (double?)t.Minutes) ?? 0;
            var used = await _db.Sessions.Where(s => s.UserId == userId).SumAsync(s => (double?)s.MinutesConsumed) ?? 0;
            return Math.Max(0, credits - used);
        }

        /// <summary>What the player can still play: the passes' allowance plus the hours bought.</summary>
        public async Task<double> GetMinutesAsync(int userId)
        {
            var passes = await GetActivePassesAsync(userId, DateTime.UtcNow);
            return await GetPackMinutesAsync(userId) + passes.Sum(p => p.AllowedMinutes);
        }

        /// <summary>
        /// Passes valid at <paramref name="now"/>, best first (unlimited before quota, then the one ending first).
        /// An unlimited pass is limited by its daily cap and its end; a quota pass by what is left of its quota and its end.
        /// </summary>
        public async Task<List<ActivePass>> GetActivePassesAsync(int userId, DateTime now)
        {
            var subs = await _db.Subscriptions.Where(s => s.UserId == userId && s.StartsAt <= now && s.EndsAt > now).ToListAsync();
            if (subs.Count == 0) return new List<ActivePass>();

            var ids = subs.Select(s => s.Id).ToList();
            var usage = await _db.PassUsage.Where(u => ids.Contains(u.SubscriptionId)).ToListAsync();
            var dayStart = DayStartUtc(now);

            var result = new List<ActivePass>();
            foreach (var s in subs)
            {
                var untilEnd = (s.EndsAt - now).TotalMinutes;
                var mine = usage.Where(u => u.SubscriptionId == s.Id).ToList();

                double allowed;
                if (s.Kind == OfferKind.QuotaPass)
                    allowed = Math.Min(untilEnd, s.QuotaMinutes - mine.Sum(u => u.Minutes));
                else if (s.DailyLimitMinutes > 0)
                    allowed = Math.Min(untilEnd, s.DailyLimitMinutes - mine.Where(u => u.At >= dayStart).Sum(u => u.Minutes));
                else
                    allowed = untilEnd;

                result.Add(new ActivePass(s, Math.Max(0, allowed)));
            }

            return result.OrderBy(p => p.Subscription.Kind == OfferKind.UnlimitedPass ? 0 : 1).ThenBy(p => p.Subscription.EndsAt).ToList();
        }

        /// <summary>Start of the café's current day (server local time), in UTC.</summary>
        public static DateTime DayStartUtc(DateTime nowUtc) => nowUtc.ToLocalTime().Date.ToUniversalTime();

        /// <summary>
        /// Charges the time since the last heartbeat: first to the player's passes, the rest to the hours bought.
        /// A long silence (crash, network loss) is billed at most <see cref="SessionRules.MaxBilledGap"/>.
        /// </summary>
        public async Task BillAsync(GameSession session)
        {
            var now = DateTime.UtcNow;
            var gap = now - session.LastHeartbeatAt;
            if (gap > SessionRules.MaxBilledGap) gap = SessionRules.MaxBilledGap;
            session.LastHeartbeatAt = now;
            if (gap <= TimeSpan.Zero) return;

            var remaining = gap.TotalMinutes;
            foreach (var pass in await GetActivePassesAsync(session.UserId, now))
            {
                if (remaining <= 0) break;
                var covered = Math.Min(remaining, pass.AllowedMinutes);
                if (covered <= 0) continue;

                _db.PassUsage.Add(new PassUsage { SubscriptionId = pass.Subscription.Id, Minutes = covered, At = now });
                remaining -= covered;
            }

            if (remaining > 0) session.MinutesConsumed += remaining;
        }
    }
}
