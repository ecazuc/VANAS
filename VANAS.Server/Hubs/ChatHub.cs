using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Server.Services;

namespace VANAS.Server.Hubs
{
    /// <summary>
    /// Live delivery of chat messages. Sending always goes through the REST API (where the rules are applied);
    /// the hub only pushes the stored message to the people who should see it.
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        public const string Path = "/hubs/chat";
        public const string MessageMethod = "Message";

        public static string RoomGroup(int gameId) => $"room-{gameId}";

        private readonly AppDbContext _db;
        private readonly ChatPresence _presence;
        private readonly AgeService _age;

        public ChatHub(AppDbContext db, ChatPresence presence, AgeService age)
        {
            _db = db;
            _presence = presence;
            _age = age;
        }

        private int UserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Starts receiving a game's room, only if the player plays that game, is not banned and may chat.</summary>
        public async Task<bool> JoinRoom(int gameId)
        {
            var userId = UserId;
            var plays = await _db.PlayerGames.AnyAsync(p => p.UserId == userId && p.GameId == gameId);
            var banned = await _db.ChatSanctions.AnyAsync(s => s.UserId == userId && s.IsBanned);
            var accepted = await _db.Profiles.AnyAsync(p => p.UserId == userId && p.ChatRulesAcceptedAt != null);
            if (!plays || banned || !accepted || await _age.ConsentPendingAsync(_db, userId)) return false;

            await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(gameId));
            _presence.Joined(Context.ConnectionId, userId, gameId);
            return true;
        }

        public async Task LeaveRoom(int gameId)
        {
            _presence.Left(Context.ConnectionId, gameId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(gameId));
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _presence.Disconnected(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
