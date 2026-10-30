using System.Security.Claims;
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
    [Route("api/sessions")]
    public class SessionsController : ControllerBase
    {
        /// <summary>Max time billed per heartbeat, so a long gap (crash, network loss) is not charged in full.</summary>
        private static readonly TimeSpan MaxBilledGap = TimeSpan.FromSeconds(90);

        private readonly AppDbContext _db;
        private readonly BalanceService _balance;

        public SessionsController(AppDbContext db, BalanceService balance)
        {
            _db = db;
            _balance = balance;
        }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartSessionRequest request)
        {
            var name = request.StationName.Trim();
            if (name.Length is 0 or > 64) return BadRequest("Invalid station name.");

            var left = await _balance.GetMinutesAsync(UserId);
            if (left <= 0) return StatusCode(StatusCodes.Status402PaymentRequired, "No time left.");

            var now = DateTime.UtcNow;
            foreach (var open in await _db.Sessions.Where(s => s.UserId == UserId && s.EndedAt == null).ToListAsync())
            {
                open.EndedAt = now;
                open.EndReason = SessionEndReason.Replaced;
            }

            var station = await _db.Stations.FirstOrDefaultAsync(s => s.Name == name);
            if (station == null)
            {
                station = new Station { Name = name };
                _db.Stations.Add(station);
            }
            station.LastSeenAt = now;
            station.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var session = new GameSession { UserId = UserId, Station = station, StartedAt = now, LastHeartbeatAt = now };
            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();
            return Ok(new SessionInfo(session.Id, left));
        }

        [HttpPost("{id:int}/heartbeat")]
        public async Task<IActionResult> Heartbeat(int id)
        {
            var session = await FindOpenSession(id);
            if (session == null) return NotFound("Session not found or already ended.");

            Bill(session);
            await _db.SaveChangesAsync();
            var left = await _balance.GetMinutesAsync(UserId);

            var status = SessionStatus.Active;
            if (session.KickRequested)
            {
                status = SessionStatus.Kicked;
                Close(session, SessionEndReason.Kicked);
            }
            else if (left <= 0)
            {
                status = SessionStatus.TimeExpired;
                Close(session, SessionEndReason.TimeExpired);
            }

            await _db.SaveChangesAsync();
            return Ok(new HeartbeatResponse(status, left));
        }

        [HttpPost("{id:int}/end")]
        public async Task<IActionResult> End(int id)
        {
            var session = await FindOpenSession(id);
            if (session == null) return NotFound("Session not found or already ended.");

            Bill(session);
            Close(session, SessionEndReason.UserLogout);
            await _db.SaveChangesAsync();
            return Ok(new { MinutesLeft = await _balance.GetMinutesAsync(UserId) });
        }

        private Task<GameSession?> FindOpenSession(int id) =>
            _db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == UserId && s.EndedAt == null);

        private static void Bill(GameSession session)
        {
            var now = DateTime.UtcNow;
            var gap = now - session.LastHeartbeatAt;
            if (gap > MaxBilledGap) gap = MaxBilledGap;
            if (gap > TimeSpan.Zero) session.MinutesConsumed += gap.TotalMinutes;
            session.LastHeartbeatAt = now;
        }

        private static void Close(GameSession session, SessionEndReason reason)
        {
            session.EndedAt = DateTime.UtcNow;
            session.EndReason = reason;
        }
    }
}
