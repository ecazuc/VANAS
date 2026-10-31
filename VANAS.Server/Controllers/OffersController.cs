using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/offers")]
    public class OffersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OffersController(AppDbContext db) => _db = db;

        internal static OfferDto ToDto(Offer o) =>
            new(o.Id, o.Name, o.Description, o.Kind, o.Minutes, o.DurationDays, o.DailyLimitMinutes, o.PriceCents, o.IsActive, o.SortOrder);

        /// <summary>The price list shown on the website and in the apps.</summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetActive() =>
            Ok((await _db.Offers.Where(o => o.IsActive).OrderBy(o => o.SortOrder).ThenBy(o => o.PriceCents).ToListAsync()).Select(ToDto));

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage")]
        public async Task<IActionResult> GetAll() =>
            Ok((await _db.Offers.OrderBy(o => o.SortOrder).ThenBy(o => o.PriceCents).ToListAsync()).Select(ToDto));

        // Only an admin changes prices; staff just sell what is on the list.
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveOfferRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var offer = new Offer();
            Apply(offer, request);
            _db.Offers.Add(offer);
            await _db.SaveChangesAsync();
            return Ok(ToDto(offer));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveOfferRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var offer = await _db.Offers.FindAsync(id);
            if (offer == null) return NotFound("Formule introuvable.");

            Apply(offer, request);
            await _db.SaveChangesAsync();
            return Ok(ToDto(offer));
        }

        /// <summary>Past sales keep their own copy of the name and price, so deleting an offer does not change the books.</summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var offer = await _db.Offers.FindAsync(id);
            if (offer == null) return NotFound("Formule introuvable.");
            _db.Offers.Remove(offer);
            await _db.SaveChangesAsync();
            return Ok();
        }

        private static void Apply(Offer o, SaveOfferRequest r)
        {
            o.Name = r.Name.Trim();
            o.Description = (r.Description ?? string.Empty).Trim();
            o.Kind = r.Kind;
            o.PriceCents = r.PriceCents;
            o.IsActive = r.IsActive;
            o.SortOrder = r.SortOrder;

            // Keep only the fields that mean something for this kind of offer.
            o.Minutes = r.Kind is OfferKind.UnlimitedPass ? 0 : r.Minutes;
            o.DurationDays = r.Kind is OfferKind.HourPack ? 0 : r.DurationDays;
            o.DailyLimitMinutes = r.Kind is OfferKind.UnlimitedPass ? r.DailyLimitMinutes : 0;
        }

        private static string? Validate(SaveOfferRequest r)
        {
            if (!Enum.IsDefined(r.Kind)) return "Type de formule invalide.";
            if ((r.Name?.Trim().Length ?? 0) is < 2 or > 60) return "Le nom doit faire entre 2 et 60 caractères.";
            if ((r.Description?.Length ?? 0) > 300) return "La description est limitée à 300 caractères.";
            if (r.PriceCents is < 0 or > 1_000_000) return "Le prix est invalide.";
            if (r.Kind != OfferKind.UnlimitedPass && r.Minutes is < 1 or > 600_000) return "La durée en minutes est invalide.";
            if (r.Kind != OfferKind.HourPack && r.DurationDays is < 1 or > 366) return "La validité doit être comprise entre 1 et 366 jours.";
            if (r.Kind == OfferKind.UnlimitedPass && r.DailyLimitMinutes is < 0 or > 1440) return "La limite par jour doit être comprise entre 0 et 1440 minutes.";
            return null;
        }
    }
}
