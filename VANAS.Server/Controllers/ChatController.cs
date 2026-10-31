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
    [Authorize]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private const int PageSize = 50;
        private const int MaxMessagesPer10Seconds = 6;

        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;
        private readonly string[] _bannedWords;
        private readonly ChatPresence _presence;
        private readonly AgeService _age;

        public ChatController(AppDbContext db, IHubContext<ChatHub> hub, IConfiguration config, ChatPresence presence, AgeService age)
        {
            _db = db;
            _hub = hub;
            _presence = presence;
            _age = age;
            _bannedWords = config.GetSection("Chat:BannedWords").Get<string[]>() ?? Array.Empty<string>();
        }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ---------- Status and rules ----------

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus() => Ok(await StatusAsync(UserId));

        [HttpPost("rules/accept")]
        public async Task<IActionResult> AcceptRules()
        {
            var profile = await _db.Profiles.FindAsync(UserId);
            if (profile == null) return NotFound();
            profile.ChatRulesAcceptedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(await StatusAsync(UserId));
        }

        private async Task<ChatStatus> StatusAsync(int userId)
        {
            var profile = await _db.Profiles.FindAsync(userId);
            var sanction = await _db.ChatSanctions.FindAsync(userId);
            var mutedUntil = sanction?.MutedUntil > DateTime.UtcNow ? sanction.MutedUntil : null;
            return new ChatStatus(profile?.ChatRulesAcceptedAt != null, sanction?.IsBanned == true, mutedUntil, await _age.ConsentPendingAsync(_db, userId));
        }

        /// <summary>Reading needs the rules accepted and no ban; writing also needs no active mute.</summary>
        private async Task<IActionResult?> RefuseAsync(bool forWriting)
        {
            var status = await StatusAsync(UserId);
            if (status.ConsentPending) return StatusCode(StatusCodes.Status403Forbidden, AgeService.ConsentMessage);
            if (!status.RulesAccepted) return StatusCode(StatusCodes.Status403Forbidden, "Acceptez d'abord les règles de la messagerie.");
            if (status.IsBanned) return StatusCode(StatusCodes.Status403Forbidden, "L'accès à la messagerie vous a été retiré.");
            if (forWriting && status.MutedUntil != null)
                return StatusCode(StatusCodes.Status403Forbidden, $"Vous ne pouvez pas écrire jusqu'au {status.MutedUntil.Value.ToLocalTime():dd/MM à HH:mm}.");
            return null;
        }

        // ---------- My games, suggestions ----------

        [HttpGet("games/mine")]
        public async Task<IActionResult> MyGames() =>
            Ok(await _db.PlayerGames.Where(p => p.UserId == UserId).Select(p => p.GameId).ToListAsync());

        [HttpPut("games/mine")]
        public async Task<IActionResult> SetMyGames([FromBody] SetGamesRequest request)
        {
            var ids = request.GameIds.Distinct().ToList();
            if (ids.Count > 30) return BadRequest("Trop de jeux sélectionnés.");

            var valid = await _db.Games.Where(g => g.IsEnabled && ids.Contains(g.Id)).Select(g => g.Id).ToListAsync();
            if (valid.Count != ids.Count) return BadRequest("Un des jeux n'existe pas.");

            var mine = await _db.PlayerGames.Where(p => p.UserId == UserId).ToListAsync();
            var dropped = mine.Where(p => !valid.Contains(p.GameId)).Select(p => p.GameId).ToList();
            _db.PlayerGames.RemoveRange(mine.Where(p => !valid.Contains(p.GameId)));
            _db.PlayerGames.AddRange(valid.Where(id => mine.All(p => p.GameId != id)).Select(id => new PlayerGame { UserId = UserId, GameId = id }));
            await _db.SaveChangesAsync();
            foreach (var gameId in dropped) await StopFeedAsync(UserId, gameId);
            return Ok(valid);
        }

        /// <summary>Players who accepted the chat rules and play at least one of my games, except blocked or banned ones.</summary>
        [HttpGet("suggestions")]
        public async Task<IActionResult> Suggestions()
        {
            var refusal = await RefuseAsync(forWriting: false);
            if (refusal != null) return refusal;

            var me = UserId;
            var myGames = await _db.PlayerGames.Where(p => p.UserId == me).Select(p => p.GameId).ToListAsync();
            if (myGames.Count == 0) return Ok(new List<PlayerSuggestion>());

            var hidden = await HiddenUserIdsAsync(me);
            var shared = await _db.PlayerGames
                .Where(p => p.UserId != me && myGames.Contains(p.GameId) && !hidden.Contains(p.UserId))
                .Join(_db.Profiles.Where(pr => pr.ChatRulesAcceptedAt != null), p => p.UserId, pr => pr.UserId, (p, pr) => new { p.UserId, p.GameId, pr.DisplayName, pr.AvatarKey })
                .Join(_db.Games, x => x.GameId, g => g.Id, (x, g) => new { x.UserId, x.DisplayName, x.AvatarKey, g.Title })
                .ToListAsync();

            var result = shared.GroupBy(x => x.UserId)
                .Select(g => new PlayerSuggestion(g.Key, g.First().DisplayName, g.First().AvatarKey, g.Select(x => x.Title).OrderBy(t => t).ToList()))
                .OrderByDescending(s => s.SharedGames.Count).ThenBy(s => s.DisplayName)
                .Take(50).ToList();
            return Ok(result);
        }

        /// <summary>Players I blocked, players who blocked me, and banned players: never shown to me and never reachable.</summary>
        private async Task<HashSet<int>> HiddenUserIdsAsync(int me)
        {
            var blocked = await _db.UserBlocks.Where(b => b.BlockerId == me).Select(b => b.BlockedId).ToListAsync();
            var blockedBy = await _db.UserBlocks.Where(b => b.BlockedId == me).Select(b => b.BlockerId).ToListAsync();
            var banned = await _db.ChatSanctions.Where(s => s.IsBanned).Select(s => s.UserId).ToListAsync();
            return blocked.Concat(blockedBy).Concat(banned).ToHashSet();
        }

        // ---------- Rooms ----------

        [HttpGet("rooms/{gameId:int}/messages")]
        public async Task<IActionResult> RoomMessages(int gameId, [FromQuery] int afterId = 0)
        {
            var refusal = await RefuseAsync(forWriting: false);
            if (refusal != null) return refusal;
            if (!await PlaysAsync(UserId, gameId)) return StatusCode(StatusCodes.Status403Forbidden, "Ajoutez ce jeu à vos jeux pour entrer dans son salon.");

            var blocked = await _db.UserBlocks.Where(b => b.BlockerId == UserId).Select(b => b.BlockedId).ToListAsync();
            var query = _db.ChatMessages.Where(m => m.GameId == gameId && !m.IsHidden && m.Id > afterId && !blocked.Contains(m.SenderId));
            var rows = afterId > 0
                ? await query.OrderBy(m => m.Id).Take(PageSize).ToListAsync()
                : (await query.OrderByDescending(m => m.Id).Take(PageSize).ToListAsync()).AsEnumerable().Reverse().ToList();
            return Ok(await ToDtosAsync(rows));
        }

        [HttpPost("rooms/{gameId:int}/messages")]
        public async Task<IActionResult> SendToRoom(int gameId, [FromBody] SendMessageRequest request)
        {
            var refusal = await RefuseAsync(forWriting: true);
            if (refusal != null) return refusal;
            if (!await PlaysAsync(UserId, gameId)) return StatusCode(StatusCodes.Status403Forbidden, "Ajoutez ce jeu à vos jeux pour écrire dans son salon.");

            var created = await SaveAsync(new ChatMessage { GameId = gameId, SenderId = UserId }, request.Text);
            if (created.Error != null) return created.Error;

            await _hub.Clients.Group(ChatHub.RoomGroup(gameId)).SendAsync(ChatHub.MessageMethod, created.Dto);
            return Ok(created.Dto);
        }

        private Task<bool> PlaysAsync(int userId, int gameId) => _db.PlayerGames.AnyAsync(p => p.UserId == userId && p.GameId == gameId);

        // ---------- Private conversations ----------

        [HttpGet("conversations")]
        public async Task<IActionResult> Conversations()
        {
            var refusal = await RefuseAsync(forWriting: false);
            if (refusal != null) return refusal;

            var me = UserId;
            var hidden = await HiddenUserIdsAsync(me);
            var rows = await _db.ChatMessages
                .Where(m => !m.IsHidden && m.GameId == null && (m.SenderId == me || m.RecipientId == me))
                .OrderByDescending(m => m.Id).Take(500).ToListAsync();

            var latest = rows.GroupBy(m => m.SenderId == me ? m.RecipientId!.Value : m.SenderId)
                .Where(g => !hidden.Contains(g.Key))
                .Select(g => g.First()).ToList();

            var otherIds = latest.Select(m => m.SenderId == me ? m.RecipientId!.Value : m.SenderId).ToList();
            var profiles = await _db.Profiles.Where(p => otherIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId);
            return Ok(latest.Select(m =>
            {
                var other = m.SenderId == me ? m.RecipientId!.Value : m.SenderId;
                var p = profiles.GetValueOrDefault(other);
                return new ConversationDto(other, p?.DisplayName ?? "?", p?.AvatarKey ?? Avatars.Default, m.Text, m.CreatedAt);
            }).OrderByDescending(c => c.LastAt));
        }

        [HttpGet("direct/{userId:int}/messages")]
        public async Task<IActionResult> DirectMessages(int userId, [FromQuery] int afterId = 0)
        {
            var refusal = await RefuseAsync(forWriting: false);
            if (refusal != null) return refusal;
            var me = UserId;
            if ((await HiddenUserIdsAsync(me)).Contains(userId)) return Ok(new List<ChatMessageDto>());

            var query = _db.ChatMessages.Where(m => m.GameId == null && !m.IsHidden && m.Id > afterId &&
                ((m.SenderId == me && m.RecipientId == userId) || (m.SenderId == userId && m.RecipientId == me)));
            var rows = afterId > 0
                ? await query.OrderBy(m => m.Id).Take(PageSize).ToListAsync()
                : (await query.OrderByDescending(m => m.Id).Take(PageSize).ToListAsync()).AsEnumerable().Reverse().ToList();
            return Ok(await ToDtosAsync(rows));
        }

        [HttpPost("direct/{userId:int}/messages")]
        public async Task<IActionResult> SendDirect(int userId, [FromBody] SendMessageRequest request)
        {
            var refusal = await RefuseAsync(forWriting: true);
            if (refusal != null) return refusal;

            var me = UserId;
            if (userId == me) return BadRequest("Vous ne pouvez pas vous écrire à vous-même.");

            var profile = await _db.Profiles.FindAsync(userId);
            // One generic answer whether the player does not exist, did not opt in, blocked me or is banned.
            const string unreachable = "Ce joueur n'est pas joignable.";
            if (profile?.ChatRulesAcceptedAt == null || (await HiddenUserIdsAsync(me)).Contains(userId)) return StatusCode(StatusCodes.Status403Forbidden, unreachable);

            // Private messages are only possible between players who share a game (or already talked).
            var shareGame = await _db.PlayerGames.Where(p => p.UserId == me)
                .AnyAsync(mine => _db.PlayerGames.Any(other => other.UserId == userId && other.GameId == mine.GameId));
            var alreadyTalked = await _db.ChatMessages.AnyAsync(m => m.GameId == null && m.SenderId == userId && m.RecipientId == me);
            if (!shareGame && !alreadyTalked) return StatusCode(StatusCodes.Status403Forbidden, unreachable);

            var created = await SaveAsync(new ChatMessage { SenderId = me, RecipientId = userId }, request.Text);
            if (created.Error != null) return created.Error;

            await _hub.Clients.Users(new[] { me.ToString(), userId.ToString() }).SendAsync(ChatHub.MessageMethod, created.Dto);
            return Ok(created.Dto);
        }

        // ---------- Saving a message ----------

        private async Task<(ChatMessageDto? Dto, IActionResult? Error)> SaveAsync(ChatMessage message, string? text)
        {
            var clean = ChatRules.Check(text, _bannedWords, out var reason);
            if (clean == null) return (null, BadRequest(reason));

            var since = DateTime.UtcNow.AddSeconds(-10);
            if (await _db.ChatMessages.CountAsync(m => m.SenderId == message.SenderId && m.CreatedAt > since) >= MaxMessagesPer10Seconds)
                return (null, StatusCode(StatusCodes.Status429TooManyRequests, "Doucement : attendez quelques secondes."));

            message.Text = clean;
            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();
            return ((await ToDtosAsync(new List<ChatMessage> { message })).Single(), null);
        }

        private async Task<List<ChatMessageDto>> ToDtosAsync(List<ChatMessage> messages)
        {
            var ids = messages.Select(m => m.SenderId).Distinct().ToList();
            var profiles = await _db.Profiles.Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId);
            return messages.Select(m =>
            {
                var p = profiles.GetValueOrDefault(m.SenderId);
                return new ChatMessageDto(m.Id, m.GameId, m.SenderId, p?.DisplayName ?? "?", p?.AvatarKey ?? Avatars.Default, m.RecipientId, m.Text, m.CreatedAt);
            }).ToList();
        }

        // ---------- Blocks and reports ----------

        [HttpGet("blocks")]
        public async Task<IActionResult> Blocks()
        {
            var me = UserId;
            return Ok(await (from b in _db.UserBlocks.Where(b => b.BlockerId == me)
                             join p in _db.Profiles on b.BlockedId equals p.UserId
                             select new BlockedPlayer(p.UserId, p.DisplayName)).ToListAsync());
        }

        [HttpPost("blocks/{userId:int}")]
        public async Task<IActionResult> Block(int userId)
        {
            var me = UserId;
            if (userId == me) return BadRequest("Vous ne pouvez pas vous bloquer vous-même.");
            if (await _db.Profiles.FindAsync(userId) == null) return NotFound("Joueur introuvable.");
            if (!await _db.UserBlocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == userId))
            {
                _db.UserBlocks.Add(new UserBlock { BlockerId = me, BlockedId = userId });
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpDelete("blocks/{userId:int}")]
        public async Task<IActionResult> Unblock(int userId)
        {
            var block = await _db.UserBlocks.FindAsync(UserId, userId);
            if (block != null)
            {
                _db.UserBlocks.Remove(block);
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost("messages/{messageId:int}/report")]
        public async Task<IActionResult> Report(int messageId, [FromBody] ReportRequest request)
        {
            var reason = (request.Reason ?? string.Empty).Trim();
            if (reason.Length is 0 or > 300) return BadRequest("Indiquez un motif (300 caractères maximum).");

            var me = UserId;
            var message = await _db.ChatMessages.FindAsync(messageId);
            var visible = message != null && !message.IsHidden && message.SenderId != me &&
                (message.GameId != null ? await PlaysAsync(me, message.GameId.Value) : message.RecipientId == me);
            if (!visible) return NotFound("Message introuvable.");

            if (!await _db.MessageReports.AnyAsync(r => r.MessageId == messageId && r.ReporterId == me))
            {
                _db.MessageReports.Add(new MessageReport { MessageId = messageId, ReporterId = me, Reason = reason });
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        // ---------- Moderation (staff) ----------

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("moderation/reports")]
        public async Task<IActionResult> OpenReports()
        {
            var reports = await _db.MessageReports.Where(r => r.ResolvedAt == null).OrderBy(r => r.CreatedAt).ToListAsync();
            var messageIds = reports.Select(r => r.MessageId).ToList();
            var messages = await _db.ChatMessages.Where(m => messageIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
            var userIds = reports.Select(r => r.ReporterId).Concat(messages.Values.Select(m => m.SenderId)).Distinct().ToList();
            var names = await _db.Profiles.Where(p => userIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName);
            var gameIds = messages.Values.Where(m => m.GameId != null).Select(m => m.GameId!.Value).Distinct().ToList();
            var games = await _db.Games.Where(g => gameIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Title);

            return Ok(reports.Where(r => messages.ContainsKey(r.MessageId)).Select(r =>
            {
                var m = messages[r.MessageId];
                var context = m.GameId != null ? $"Salon {games.GetValueOrDefault(m.GameId.Value, "?")}" : "Message privé";
                return new ReportDto(r.Id, r.CreatedAt, m.Id, m.Text, m.IsHidden, m.SenderId, names.GetValueOrDefault(m.SenderId, "?"),
                    names.GetValueOrDefault(r.ReporterId, "?"), r.Reason, context);
            }));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("moderation/reports/{reportId:int}/resolve")]
        public async Task<IActionResult> Resolve(int reportId, [FromBody] ModerateRequest request)
        {
            if (!Enum.IsDefined(request.Action)) return BadRequest("Action invalide.");
            var report = await _db.MessageReports.FindAsync(reportId);
            if (report == null || report.ResolvedAt != null) return NotFound("Signalement introuvable.");

            var message = await _db.ChatMessages.FindAsync(report.MessageId);
            var now = DateTime.UtcNow;
            int? bannedUserId = null;

            if (message != null && request.Action != ModerationAction.Dismiss) message.IsHidden = true;
            if (message != null && request.Action is ModerationAction.Mute24Hours or ModerationAction.Mute7Days or ModerationAction.Ban)
            {
                var sanction = await _db.ChatSanctions.FindAsync(message.SenderId);
                if (sanction == null)
                {
                    sanction = new ChatSanction { UserId = message.SenderId };
                    _db.ChatSanctions.Add(sanction);
                }

                sanction.Reason = report.Reason;
                if (request.Action == ModerationAction.Ban) { sanction.IsBanned = true; bannedUserId = message.SenderId; }
                else sanction.MutedUntil = now.AddDays(request.Action == ModerationAction.Mute7Days ? 7 : 1);
            }

            // Every open report about the same message is settled by the same decision.
            foreach (var r in await _db.MessageReports.Where(r => r.MessageId == report.MessageId && r.ResolvedAt == null).ToListAsync())
            {
                r.ResolvedAt = now;
                r.Resolution = request.Action.ToString();
            }

            await _db.SaveChangesAsync();

            // A banned player stops receiving the rooms right now, not only at their next reconnection.
            if (bannedUserId is int banned) await StopFeedAsync(banned, null);
            return Ok();
        }

        /// <summary>Removes a player's live connections from one room (or all rooms when gameId is null).</summary>
        private async Task StopFeedAsync(int userId, int? gameId)
        {
            foreach (var (connectionId, room) in _presence.Remove(userId, gameId))
                await _hub.Groups.RemoveFromGroupAsync(connectionId, ChatHub.RoomGroup(room));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("moderation/sanctions")]
        public async Task<IActionResult> Sanctions()
        {
            var now = DateTime.UtcNow;
            return Ok(await (from s in _db.ChatSanctions.Where(s => s.IsBanned || s.MutedUntil > now)
                             join p in _db.Profiles on s.UserId equals p.UserId
                             select new SanctionDto(s.UserId, p.DisplayName, s.MutedUntil, s.IsBanned, s.Reason)).ToListAsync());
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpDelete("moderation/sanctions/{userId:int}")]
        public async Task<IActionResult> LiftSanction(int userId)
        {
            var sanction = await _db.ChatSanctions.FindAsync(userId);
            if (sanction != null)
            {
                _db.ChatSanctions.Remove(sanction);
                await _db.SaveChangesAsync();
            }
            return Ok();
        }
    }
}
