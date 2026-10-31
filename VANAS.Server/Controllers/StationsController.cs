using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Server.Hubs;
using VANAS.Server.Services;
using VANAS.Shared.Contracts;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Authorize(Roles = "Staff,Admin")]
    [Route("api/stations")]
    public class StationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly BalanceService _balance;
        private readonly IHubContext<SessionHub> _hub;

        public StationsController(AppDbContext db, BalanceService balance, IHubContext<SessionHub> hub)
        {
            _db = db;
            _balance = balance;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var stations = await _db.Stations.OrderBy(s => s.Name).ToListAsync();
            var cutoff = SessionRules.LiveCutoff;
            var live = await _db.Sessions
                .Where(s => s.EndedAt == null && s.LastHeartbeatAt > cutoff)
                .Join(_db.Users, s => s.UserId, u => u.Id, (s, u) => new { s.StationId, s.UserId, u.Username, s.StartedAt })
                .ToListAsync();

            var result = new List<StationStatus>();
            foreach (var st in stations)
            {
                var session = live.FirstOrDefault(l => l.StationId == st.Id);
                result.Add(session == null
                    ? new StationStatus(st.Id, st.Name, st.IpAddress, st.LastSeenAt, false, null, null, null, null)
                    : new StationStatus(st.Id, st.Name, st.IpAddress, st.LastSeenAt, true, session.UserId, session.Username,
                        await _balance.GetMinutesAsync(session.UserId), session.StartedAt));
            }
            return Ok(result);
        }

        /// <summary>Sends a short message to the player using a PC. It appears on their screen within seconds.</summary>
        [HttpPost("{id:int}/message")]
        public async Task<IActionResult> SendMessage(int id, [FromBody] StationMessageRequest request)
        {
            var text = (request.Text ?? string.Empty).Trim();
            if (text.Length is 0 or > 200) return BadRequest("Le message doit faire entre 1 et 200 caractères.");

            var station = await _db.Stations.FindAsync(id);
            if (station == null) return NotFound("Poste introuvable.");

            var cutoff = SessionRules.LiveCutoff;
            var userId = await _db.Sessions
                .Where(s => s.StationId == id && s.EndedAt == null && s.LastHeartbeatAt > cutoff)
                .Select(s => (int?)s.UserId).FirstOrDefaultAsync();
            if (userId == null) return BadRequest("Ce poste n'est pas utilisé en ce moment.");

            station.PendingMessage = text;
            station.PendingMessageAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Nudge the PC so it syncs right away instead of at its next heartbeat.
            await _hub.Clients.User(userId.Value.ToString()).SendAsync(SessionHub.RefreshMethod);
            return Ok();
        }
    }
}
