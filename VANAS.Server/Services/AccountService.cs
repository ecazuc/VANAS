using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Models;

namespace VANAS.Server.Services
{
    /// <summary>Everything a player can ask about their own data: a copy of it (access) and its deletion (erasure).</summary>
    public class AccountService
    {
        public const string DeletedName = "(compte supprimé)";

        private readonly AppDbContext _db;
        private readonly AgeService _age;

        public AccountService(AppDbContext db, AgeService age)
        {
            _db = db;
            _age = age;
        }

        /// <summary>
        /// Deletes the account and the personal data attached to it. Sales stay in the books (accounting duty, 10 years)
        /// but lose the player's name and any free-text note.
        /// </summary>
        public async Task<bool> DeleteAsync(int userId)
        {
            var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            var messageIds = await _db.ChatMessages.Where(m => m.SenderId == userId || m.RecipientId == userId).Select(m => m.Id).ToListAsync();
            _db.MessageReports.RemoveRange(_db.MessageReports.Where(r => r.ReporterId == userId || messageIds.Contains(r.MessageId)));
            _db.ChatMessages.RemoveRange(_db.ChatMessages.Where(m => messageIds.Contains(m.Id)));
            _db.UserBlocks.RemoveRange(_db.UserBlocks.Where(b => b.BlockerId == userId || b.BlockedId == userId));
            _db.PlayerGames.RemoveRange(_db.PlayerGames.Where(p => p.UserId == userId));
            _db.ChatSanctions.RemoveRange(_db.ChatSanctions.Where(s => s.UserId == userId));

            _db.Sessions.RemoveRange(_db.Sessions.Where(s => s.UserId == userId));
            _db.TimeLedger.RemoveRange(_db.TimeLedger.Where(t => t.UserId == userId));
            var subscriptionIds = await _db.Subscriptions.Where(s => s.UserId == userId).Select(s => s.Id).ToListAsync();
            _db.PassUsage.RemoveRange(_db.PassUsage.Where(u => subscriptionIds.Contains(u.SubscriptionId)));
            _db.Subscriptions.RemoveRange(_db.Subscriptions.Where(s => s.UserId == userId));

            foreach (var sale in await _db.Sales.Where(s => s.UserId == userId).ToListAsync())
            {
                sale.UserName = DeletedName;
                sale.Note = null;
            }

            // Event registrations go with the user (cascade), which also frees the seats.
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>A readable copy of what is stored about the player. Other people's messages are not included: only what the player wrote.</summary>
        public async Task<object?> ExportAsync(int userId)
        {
            var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            var games = await (from p in _db.PlayerGames.Where(p => p.UserId == userId)
                               join g in _db.Games on p.GameId equals g.Id
                               select g.Title).ToListAsync();
            var blocked = await (from b in _db.UserBlocks.Where(b => b.BlockerId == userId)
                                 join pr in _db.Profiles on b.BlockedId equals pr.UserId
                                 select pr.DisplayName).ToListAsync();
            var gameTitles = await _db.Games.ToDictionaryAsync(g => g.Id, g => g.Title);

            return new
            {
                ExportedAt = DateTime.UtcNow,
                Account = new
                {
                    user.Username,
                    DisplayName = user.Profile?.DisplayName,
                    Avatar = user.Profile?.AvatarKey,
                    user.BirthDate,
                    Role = user.Role.ToString(),
                    MemberSince = user.CreatedAt,
                    ChatRulesAcceptedAt = user.Profile?.ChatRulesAcceptedAt,
                    ParentalConsentRecordedAt = user.ParentalConsentAt,
                    ParentalConsentRequired = _age.ConsentPending(user) || user.ParentalConsentAt != null
                },
                Sessions = await (from s in _db.Sessions.Where(s => s.UserId == userId)
                                  join st in _db.Stations on s.StationId equals st.Id
                                  orderby s.StartedAt descending
                                  select new { s.StartedAt, s.EndedAt, Station = st.Name, MinutesCharged = s.MinutesConsumed }).ToListAsync(),
                TimeCredits = await _db.TimeLedger.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt)
                    .Select(t => new { t.CreatedAt, t.Minutes, Reason = t.Reason.ToString(), t.Note }).ToListAsync(),
                Passes = await _db.Subscriptions.Where(s => s.UserId == userId).OrderByDescending(s => s.StartsAt)
                    .Select(s => new { s.OfferName, Kind = s.Kind.ToString(), s.StartsAt, s.EndsAt }).ToListAsync(),
                Purchases = await _db.Sales.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        s.CreatedAt, s.OfferName, PriceEuros = s.PriceCents / 100m, PaymentMethod = s.Method.ToString(),
                        Cancelled = s.CancelledAt != null, s.CancelReason
                    }).ToListAsync(),
                EventTickets = await (from r in _db.EventRegistrations.Where(r => r.UserId == userId)
                                      join e in _db.Events on r.EventId equals e.Id
                                      orderby e.StartsAt descending
                                      select new { Event = e.Title, e.StartsAt, r.TicketCode, RegisteredAt = r.CreatedAt, r.CheckedInAt, r.PaidAt }).ToListAsync(),
                Messaging = new
                {
                    GamesPlayed = games,
                    BlockedPlayers = blocked,
                    MessagesSent = (await _db.ChatMessages.Where(m => m.SenderId == userId && !m.IsHidden).OrderByDescending(m => m.Id).ToListAsync())
                        .Select(m => new
                        {
                            m.CreatedAt,
                            m.Text,
                            Where = m.GameId != null ? "Salon " + gameTitles.GetValueOrDefault(m.GameId.Value, "?") : "Message privé"
                        }).ToList()
                }
            };
        }
    }
}
