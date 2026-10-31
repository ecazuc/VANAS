using System.Security.Claims;
using System.Security.Cryptography;
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
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        // No 0/O/1/I so a ticket code can be read out loud at the door.
        private const string TicketAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly AppDbContext _db;
        private readonly AgeService _age;

        public EventsController(AppDbContext db, AgeService age)
        {
            _db = db;
            _age = age;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string NewTicketCode() => RandomNumberGenerator.GetString(TicketAlphabet, 10);

        private Task<int> CountAsync(int eventId) => _db.EventRegistrations.CountAsync(r => r.EventId == eventId);

        private async Task<EventSummary> ToSummaryAsync(GameEvent e) =>
            new(e.Id, e.Title, e.GameName, e.StartsAt, e.Capacity, await CountAsync(e.Id), e.PriceCents, e.IsPublished);

        private async Task<EventDetail> ToDetailAsync(GameEvent e) =>
            new(e.Id, e.Title, e.GameName, e.DescriptionMarkdown, e.StartsAt, e.Capacity, await CountAsync(e.Id), e.PriceCents, e.IsPublished);

        // ---------- Public ----------

        /// <summary>Published events that have not finished yet (an event stays listed for 6 hours after its start).</summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetUpcoming()
        {
            var since = DateTime.UtcNow.AddHours(-6);
            var events = await _db.Events.Where(e => e.IsPublished && e.StartsAt >= since).OrderBy(e => e.StartsAt).ToListAsync();
            var list = new List<EventSummary>();
            foreach (var e in events) list.Add(await ToSummaryAsync(e));
            return Ok(list);
        }

        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOne(int id)
        {
            var e = await _db.Events.FirstOrDefaultAsync(x => x.Id == id && x.IsPublished);
            return e == null ? NotFound("Événement introuvable.") : Ok(await ToDetailAsync(e));
        }

        // ---------- Customer ----------

        [Authorize]
        [HttpPost("{id:int}/register")]
        public async Task<IActionResult> Register(int id)
        {
            var e = await _db.Events.FirstOrDefaultAsync(x => x.Id == id && x.IsPublished);
            if (e == null) return NotFound("Événement introuvable.");
            if (e.StartsAt < DateTime.UtcNow) return BadRequest("Cet événement a déjà commencé.");
            if (await _age.ConsentPendingAsync(_db, CurrentUserId)) return StatusCode(StatusCodes.Status403Forbidden, AgeService.ConsentMessage);

            var userId = CurrentUserId;
            var existing = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.UserId == userId);
            if (existing != null) return Conflict("Vous êtes déjà inscrit.");
            if (await CountAsync(id) >= e.Capacity) return Conflict("Complet : il n'y a plus de place.");

            var registration = new EventRegistration { EventId = id, UserId = userId, TicketCode = NewTicketCode() };
            _db.EventRegistrations.Add(registration);
            await _db.SaveChangesAsync();
            return Ok(new TicketInfo(e.Id, e.Title, e.StartsAt, registration.TicketCode, false));
        }

        [Authorize]
        [HttpDelete("{id:int}/register")]
        public async Task<IActionResult> Unregister(int id)
        {
            var userId = CurrentUserId;
            var registration = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.UserId == userId);
            if (registration == null) return NotFound("Vous n'êtes pas inscrit.");
            if (registration.CheckedInAt != null) return BadRequest("Votre entrée est déjà validée.");

            _db.EventRegistrations.Remove(registration);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var userId = CurrentUserId;
            var rows = await (from r in _db.EventRegistrations
                              join e in _db.Events on r.EventId equals e.Id
                              where r.UserId == userId
                              orderby e.StartsAt descending
                              select new TicketInfo(e.Id, e.Title, e.StartsAt, r.TicketCode, r.CheckedInAt != null)).ToListAsync();
            return Ok(rows);
        }

        // ---------- Staff ----------

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage")]
        public async Task<IActionResult> GetAll()
        {
            var events = await _db.Events.OrderByDescending(e => e.StartsAt).ToListAsync();
            var list = new List<EventSummary>();
            foreach (var e in events) list.Add(await ToSummaryAsync(e));
            return Ok(list);
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage/{id:int}")]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var e = await _db.Events.FindAsync(id);
            return e == null ? NotFound("Événement introuvable.") : Ok(await ToDetailAsync(e));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveEventRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var e = new GameEvent();
            Apply(e, request);
            _db.Events.Add(e);
            await _db.SaveChangesAsync();
            return Ok(await ToDetailAsync(e));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveEventRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var e = await _db.Events.FindAsync(id);
            if (e == null) return NotFound("Événement introuvable.");

            var registered = await CountAsync(id);
            if (request.Capacity < registered)
                return BadRequest($"{registered} personnes sont déjà inscrites : la capacité ne peut pas descendre en dessous.");

            Apply(e, request);
            await _db.SaveChangesAsync();
            return Ok(await ToDetailAsync(e));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var e = await _db.Events.FindAsync(id);
            if (e == null) return NotFound("Événement introuvable.");
            _db.Events.Remove(e);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage/{id:int}/registrations")]
        public async Task<IActionResult> Registrations(int id)
        {
            var rows = await (from r in _db.EventRegistrations
                              join u in _db.Users.Include(x => x.Profile) on r.UserId equals u.Id
                              where r.EventId == id
                              orderby r.CreatedAt
                              select new RegistrationInfo(u.Username, u.Profile!.DisplayName, r.TicketCode, r.CreatedAt, r.CheckedInAt)).ToListAsync();
            return Ok(rows);
        }

        /// <summary>Validates a ticket at the door. The first scan succeeds, any later one is refused.</summary>
        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            var code = (request.TicketCode ?? string.Empty).Trim().ToUpperInvariant();
            var registration = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.TicketCode == code);
            if (registration == null) return NotFound("Billet inconnu.");

            if (registration.CheckedInAt != null)
                return Conflict($"Billet déjà utilisé à {registration.CheckedInAt.Value.ToLocalTime():HH:mm}.");

            registration.CheckedInAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var e = await _db.Events.FindAsync(registration.EventId);
            var profile = await _db.Profiles.FindAsync(registration.UserId);
            return Ok(new CheckInResult(profile?.DisplayName ?? "?", e?.Title ?? "?"));
        }

        private static void Apply(GameEvent e, SaveEventRequest r)
        {
            e.Title = r.Title.Trim();
            e.GameName = r.GameName.Trim();
            e.DescriptionMarkdown = r.DescriptionMarkdown ?? string.Empty;
            e.StartsAt = r.StartsAt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(r.StartsAt, DateTimeKind.Utc) : r.StartsAt.ToUniversalTime();
            e.Capacity = r.Capacity;
            e.PriceCents = r.PriceCents;
            e.IsPublished = r.IsPublished;
        }

        private static string? Validate(SaveEventRequest r)
        {
            if ((r.Title?.Trim().Length ?? 0) is < 3 or > 120) return "Le titre doit faire entre 3 et 120 caractères.";
            if ((r.GameName?.Length ?? 0) > 80) return "Le nom du jeu est limité à 80 caractères.";
            if ((r.DescriptionMarkdown?.Length ?? 0) > 20_000) return "La description est trop longue.";
            if (r.Capacity is < 1 or > 1000) return "La capacité doit être comprise entre 1 et 1000 places.";
            if (r.PriceCents is < 0 or > 100_000) return "Le tarif est invalide.";
            return null;
        }
    }
}
