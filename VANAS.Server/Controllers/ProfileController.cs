using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Server.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/profile")]
    public class ProfileController : ControllerBase
    {
        private static readonly Regex AllowedName = new(@"^[\p{L}\p{N}][\p{L}\p{N} _.\-]{1,22}[\p{L}\p{N}]$", RegexOptions.Compiled);

        private readonly AppDbContext _db;
        private readonly BalanceService _balance;

        public ProfileController(AppDbContext db, BalanceService balance)
        {
            _db = db;
            _balance = balance;
        }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        public async Task<IActionResult> GetMine()
        {
            var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == UserId);
            if (user == null) return Unauthorized();
            return Ok(await BuildAsync(user));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMine([FromBody] UpdateProfileRequest request)
        {
            var name = request.DisplayName.Trim();
            if (!AllowedName.IsMatch(name))
                return BadRequest("Le pseudo doit faire 3 à 24 caractères (lettres, chiffres, espace, _ . -).");
            if (!Avatars.IsValid(request.AvatarKey))
                return BadRequest("Avatar inconnu.");

            var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == UserId);
            if (user == null) return Unauthorized();

            var lower = name.ToLower();
            if (await _db.Profiles.AnyAsync(p => p.UserId != user.Id && p.DisplayName.ToLower() == lower))
                return BadRequest("Ce pseudo est déjà pris.");

            var profile = user.Profile ??= new Profile { UserId = user.Id };
            profile.DisplayName = name;
            profile.AvatarKey = request.AvatarKey;
            await _db.SaveChangesAsync();
            return Ok(await BuildAsync(user));
        }

        [HttpGet("me/live")]
        public async Task<IActionResult> GetLive()
        {
            var cutoff = SessionRules.LiveCutoff;
            var live = await _db.Sessions
                .Where(s => s.UserId == UserId && s.EndedAt == null && s.LastHeartbeatAt > cutoff)
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new { s.Station!.Name, s.LastHeartbeatAt })
                .FirstOrDefaultAsync();

            var minutes = await _balance.GetMinutesAsync(UserId);
            if (live == null) return Ok(new LiveStatus(minutes, false, null));

            // The balance only drops at each heartbeat: also take off the time that has passed since the last one,
            // so the phone does not jump back up between two heartbeats.
            var pending = DateTime.UtcNow - live.LastHeartbeatAt;
            if (pending > SessionRules.MaxBilledGap) pending = SessionRules.MaxBilledGap;
            return Ok(new LiveStatus(Math.Max(0, minutes - Math.Max(0, pending.TotalMinutes)), true, live.Name));
        }

        [HttpGet("me/sessions")]
        public async Task<IActionResult> GetMySessions([FromQuery] int take = 20)
        {
            take = Math.Clamp(take, 1, 50);
            var items = await _db.Sessions
                .Where(s => s.UserId == UserId)
                .OrderByDescending(s => s.StartedAt)
                .Take(take)
                .Select(s => new SessionHistoryItem(s.StartedAt, s.EndedAt, s.MinutesConsumed, s.Station!.Name, s.EndReason))
                .ToListAsync();
            return Ok(items);
        }

        private async Task<ProfileInfo> BuildAsync(User user)
        {
            var sessions = _db.Sessions.Where(s => s.UserId == user.Id);
            // Time played counts both what was taken from the hours bought and what a pass covered.
            var total = await sessions.SumAsync(s => (double?)s.MinutesConsumed) ?? 0;
            total += await (from u in _db.PassUsage
                            join s in _db.Subscriptions on u.SubscriptionId equals s.Id
                            where s.UserId == user.Id
                            select (double?)u.Minutes).SumAsync() ?? 0;
            var count = await sessions.CountAsync();

            string? passSummary = null;
            var active = await _balance.GetActivePassesAsync(user.Id, DateTime.UtcNow);
            if (active.Count > 0) passSummary = Describe(active[0]);

            return new ProfileInfo(
                user.Username,
                user.Profile?.DisplayName ?? user.Username,
                user.Profile?.AvatarKey ?? Avatars.Default,
                await _balance.GetMinutesAsync(user.Id),
                total,
                count,
                user.CreatedAt,
                passSummary);
        }

        private static string Describe(ActivePass pass)
        {
            var until = pass.Subscription.EndsAt.ToLocalTime().ToString("dd/MM/yyyy");
            return pass.Subscription.Kind == OfferKind.QuotaPass
                ? $"{pass.Subscription.OfferName} : {FormatMinutes(pass.AllowedMinutes)} restantes, jusqu'au {until}"
                : $"{pass.Subscription.OfferName} : illimité jusqu'au {until}";
        }

        private static string FormatMinutes(double minutes)
        {
            var total = (int)Math.Round(minutes);
            return total >= 60 ? $"{total / 60} h {total % 60:00}" : $"{total} min";
        }

        /// <summary>The player's active and upcoming passes.</summary>
        [HttpGet("me/subscriptions")]
        public async Task<IActionResult> GetMySubscriptions()
        {
            var now = DateTime.UtcNow;
            var current = (await _balance.GetActivePassesAsync(UserId, now)).ToDictionary(p => p.Subscription.Id);
            var subs = await _db.Subscriptions.Where(s => s.UserId == UserId && s.EndsAt > now).OrderBy(s => s.StartsAt).ToListAsync();

            return Ok(subs.Select(s =>
            {
                var live = current.GetValueOrDefault(s.Id);
                double? left = live == null ? null
                    : s.Kind == OfferKind.UnlimitedPass && s.DailyLimitMinutes == 0 ? null
                    : live.AllowedMinutes;
                return new SubscriptionInfo(s.Id, s.OfferName, s.Kind, s.StartsAt, s.EndsAt, live != null, left);
            }));
        }
    }
}
