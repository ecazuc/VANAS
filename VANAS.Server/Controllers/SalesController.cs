using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Server.Hubs;
using VANAS.Server.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/sales")]
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly BalanceService _balance;
        private readonly IHubContext<SessionHub> _hub;

        public SalesController(AppDbContext db, BalanceService balance, IHubContext<SessionHub> hub)
        {
            _db = db;
            _balance = balance;
            _hub = hub;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static SaleDto ToDto(Sale s) =>
            new(s.Id, s.CreatedAt, s.UserName, s.OfferName, s.OfferKind, s.PriceCents, s.Method, s.SoldByName, s.Note);

        /// <summary>Counter sale: the staff has been paid and credits the player with the chosen offer.</summary>
        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        public async Task<IActionResult> Sell([FromBody] SellRequest request)
        {
            if (!Enum.IsDefined(request.Method)) return BadRequest("Moyen de paiement invalide.");
            if ((request.Note?.Length ?? 0) > 200) return BadRequest("La note est limitée à 200 caractères.");

            var player = await _db.Users.FindAsync(request.UserId);
            if (player == null) return NotFound("Compte introuvable.");

            var offer = await _db.Offers.FindAsync(request.OfferId);
            if (offer == null || !offer.IsActive) return NotFound("Formule introuvable ou retirée de la vente.");

            var seller = await _db.Users.FindAsync(CurrentUserId);
            var now = DateTime.UtcNow;

            var sale = new Sale
            {
                UserId = player.Id,
                UserName = player.Username,
                OfferId = offer.Id,
                OfferName = offer.Name,
                OfferKind = offer.Kind,
                PriceCents = offer.PriceCents,
                Method = request.Method,
                SoldByUserId = CurrentUserId,
                SoldByName = seller?.Username ?? "?",
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = now
            };
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            if (offer.Kind == OfferKind.HourPack)
            {
                _db.TimeLedger.Add(new TimeLedgerEntry
                {
                    UserId = player.Id,
                    Minutes = offer.Minutes,
                    Reason = LedgerReason.Purchase,
                    Note = $"Vente #{sale.Id} : {offer.Name}",
                    CreatedByUserId = CurrentUserId,
                    CreatedAt = now
                });
            }
            else
            {
                // A second pass of the same kind starts when the previous one ends, so nothing the player paid for is wasted.
                var lastEnd = await _db.Subscriptions
                    .Where(s => s.UserId == player.Id && s.Kind == offer.Kind && s.EndsAt > now)
                    .MaxAsync(s => (DateTime?)s.EndsAt);
                var starts = lastEnd ?? now;

                _db.Subscriptions.Add(new Subscription
                {
                    UserId = player.Id,
                    SaleId = sale.Id,
                    OfferName = offer.Name,
                    Kind = offer.Kind,
                    StartsAt = starts,
                    EndsAt = starts.AddDays(offer.DurationDays),
                    QuotaMinutes = offer.Kind == OfferKind.QuotaPass ? offer.Minutes : 0,
                    DailyLimitMinutes = offer.Kind == OfferKind.UnlimitedPass ? offer.DailyLimitMinutes : 0
                });
            }

            await _db.SaveChangesAsync();
            await _hub.Clients.User(player.Id.ToString()).SendAsync(SessionHub.RefreshMethod);
            return Ok(new SellResult(ToDto(sale), await _balance.GetMinutesAsync(player.Id)));
        }

        /// <summary>The day's sales and totals by payment method (day = café local time, today by default).</summary>
        [Authorize(Roles = "Staff,Admin")]
        [HttpGet]
        public async Task<IActionResult> Report([FromQuery] DateTime? day = null)
        {
            var localDay = (day ?? DateTime.Now).Date;
            var from = DateTime.SpecifyKind(localDay, DateTimeKind.Local).ToUniversalTime();
            var to = from.AddDays(1);

            var sales = await _db.Sales.Where(s => s.CreatedAt >= from && s.CreatedAt < to).OrderByDescending(s => s.CreatedAt).ToListAsync();
            var byMethod = sales.GroupBy(s => s.Method)
                .Select(g => new MethodTotal(g.Key, g.Count(), g.Sum(s => s.PriceCents)))
                .OrderBy(m => m.Method).ToList();
            return Ok(new SalesReport(localDay, sales.Select(ToDto).ToList(), sales.Sum(s => s.PriceCents), byMethod));
        }
    }
}
