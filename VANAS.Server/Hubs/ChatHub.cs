using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;

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

        public ChatHub(AppDbContext db) => _db = db;

        /// <summary>Starts receiving a game's room, only if the player plays that game and is not banned.</summary>
        public async Task<bool> JoinRoom(int gameId)
        {
            var userId = int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var plays = await _db.PlayerGames.AnyAsync(p => p.UserId == userId && p.GameId == gameId);
            var banned = await _db.ChatSanctions.AnyAsync(s => s.UserId == userId && s.IsBanned);
            if (!plays || banned) return false;

            await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(gameId));
            return true;
        }

        public Task LeaveRoom(int gameId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(gameId));
    }
}
