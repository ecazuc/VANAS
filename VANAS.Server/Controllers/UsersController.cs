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
    [Authorize(Roles = "Staff,Admin")]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly BalanceService _balance;
        private readonly IHubContext<SessionHub> _hub;

        public UsersController(AppDbContext db, BalanceService balance, IHubContext<SessionHub> hub)
        {
            _db = db;
            _balance = balance;
            _hub = hub;
        }

        /// <summary>Tells the user's PC to sync right away (kick, added time) instead of waiting for the next heartbeat.</summary>
        private Task NotifyAsync(int userId) => _hub.Clients.User(userId.ToString()).SendAsync(SessionHub.RefreshMethod);

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
            var cutoff = SessionRules.LiveCutoff;
            var online = await _db.Sessions.Where(s => s.EndedAt == null && s.LastHeartbeatAt > cutoff).Select(s => s.UserId).ToListAsync();

            var result = new List<UserSummary>();
            foreach (var u in users)
                result.Add(new UserSummary(u.Id, u.Username, u.Role, await _balance.GetMinutesAsync(u.Id), online.Contains(u.Id)));
            return Ok(result);
        }

        [HttpPost("{id:int}/add-time")]
        public async Task<IActionResult> AddTime(int id, [FromBody] AddTimeRequest request)
        {
            if (request.Minutes == 0 || Math.Abs(request.Minutes) > 24 * 60 * 31)
                return BadRequest("Invalid amount of minutes.");
            if (!await _db.Users.AnyAsync(u => u.Id == id)) return NotFound("User not found.");

            _db.TimeLedger.Add(new TimeLedgerEntry
            {
                UserId = id,
                Minutes = request.Minutes,
                Reason = request.Minutes > 0 ? LedgerReason.Purchase : LedgerReason.Adjustment,
                Note = request.Note,
                CreatedByUserId = CurrentUserId
            });
            await _db.SaveChangesAsync();
            await NotifyAsync(id);
            return Ok(new { MinutesLeft = await _balance.GetMinutesAsync(id) });
        }

        [HttpPost("{id:int}/kick")]
        public async Task<IActionResult> Kick(int id)
        {
            var sessions = await _db.Sessions.Where(s => s.UserId == id && s.EndedAt == null).ToListAsync();
            if (sessions.Count == 0) return NotFound("No active session.");
            foreach (var s in sessions) s.KickRequested = true;
            await _db.SaveChangesAsync();
            await NotifyAsync(id);
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}/role")]
        public async Task<IActionResult> SetRole(int id, [FromBody] SetRoleRequest request)
        {
            if (id == CurrentUserId) return BadRequest("You cannot change your own role.");
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound("User not found.");
            user.Role = request.Role;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id == CurrentUserId) return BadRequest("You cannot delete yourself.");
            var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound("User not found.");
            // The player's chat data goes with the account (messages sent or received, blocks, reports, sanctions).
            var messageIds = await _db.ChatMessages.Where(m => m.SenderId == id || m.RecipientId == id).Select(m => m.Id).ToListAsync();
            _db.MessageReports.RemoveRange(_db.MessageReports.Where(r => r.ReporterId == id || messageIds.Contains(r.MessageId)));
            _db.ChatMessages.RemoveRange(_db.ChatMessages.Where(m => messageIds.Contains(m.Id)));
            _db.UserBlocks.RemoveRange(_db.UserBlocks.Where(b => b.BlockerId == id || b.BlockedId == id));
            _db.PlayerGames.RemoveRange(_db.PlayerGames.Where(p => p.UserId == id));
            _db.ChatSanctions.RemoveRange(_db.ChatSanctions.Where(s => s.UserId == id));

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
