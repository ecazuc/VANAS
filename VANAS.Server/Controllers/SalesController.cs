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
            new(s.Id, s.CreatedAt, s.UserName, s.OfferName, s.OfferKind, s.PriceCents, s.Method, s.SoldByName, s.Note,
                s.CancelledAt != null, s.CancelReason);

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

            // Cancelled sales stay listed (with their reason) but no longer count in the totals.
            var sales = await _db.Sales.Where(s => s.CreatedAt >= from && s.CreatedAt < to).OrderByDescending(s => s.CreatedAt).ToListAsync();
            var counted = sales.Where(s => s.CancelledAt == null).ToList();
            var byMethod = counted.GroupBy(s => s.Method)
                .Select(g => new MethodTotal(g.Key, g.Count(), g.Sum(s => s.PriceCents)))
                .OrderBy(m => m.Method).ToList();
            return Ok(new SalesReport(localDay, sales.Select(ToDto).ToList(), counted.Sum(s => s.PriceCents), byMethod));
        }

        /// <summary>
        /// Refund: cancels a sale and takes back what it gave. Hours bought are removed from the balance (it never goes below zero),
        /// a pass ends at once, and an event entry becomes payable again. The sale stays in the books, marked as cancelled.
        /// </summary>
        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelSaleRequest request)
        {
            var reason = (request.Reason ?? string.Empty).Trim();
            if (reason.Length is < 3 or > 200) return BadRequest("Indiquez le motif de l'annulation (3 à 200 caractères).");

            var sale = await _db.Sales.FindAsync(id);
            if (sale == null) return NotFound("Vente introuvable.");
            if (sale.CancelledAt != null) return Conflict("Cette vente est déjà annulée.");

            var now = DateTime.UtcNow;
            var seller = await _db.Users.FindAsync(CurrentUserId);

            switch (sale.OfferKind)
            {
                case OfferKind.HourPack:
                    var credit = await _db.TimeLedger.FirstOrDefaultAsync(t => t.UserId == sale.UserId && t.Note != null && t.Note.StartsWith($"Vente #{sale.Id} :"));
                    if (credit != null)
                        _db.TimeLedger.Add(new TimeLedgerEntry
                        {
                            UserId = sale.UserId,
                            Minutes = -credit.Minutes,
                            Reason = LedgerReason.Adjustment,
                            Note = $"Annulation de la vente #{sale.Id}",
                            CreatedByUserId = CurrentUserId,
                            CreatedAt = now
                        });
                    break;

                case OfferKind.UnlimitedPass:
                case OfferKind.QuotaPass:
                    var pass = await _db.Subscriptions.FirstOrDefaultAsync(s => s.SaleId == sale.Id);
                    if (pass != null)
                    {
                        // A pass not started yet disappears; one in use ends now (what it already covered stays in the history).
                        if (pass.StartsAt >= now) _db.Subscriptions.Remove(pass);
                        else if (pass.EndsAt > now) pass.EndsAt = now;
                    }
                    break;

                case OfferKind.EventTicket:
                    var entry = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.SaleId == sale.Id);
                    if (entry != null)
                    {
                        entry.PaidAt = null;
                        entry.SaleId = null;
                        entry.CheckedInAt = null;
                    }
                    break;
            }

            sale.CancelledAt = now;
            sale.CancelledByName = seller?.Username ?? "?";
            sale.CancelReason = reason;
            await _db.SaveChangesAsync();

            await _hub.Clients.User(sale.UserId.ToString()).SendAsync(SessionHub.RefreshMethod);
            return Ok(ToDto(sale));
        }
    }
}
