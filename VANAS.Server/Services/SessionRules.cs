using VANAS.Shared.Models;

namespace VANAS.Server.Services
{
    public static class SessionRules
    {
        /// <summary>A session whose PC stopped sending heartbeats is considered dead after this delay.</summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

        public static DateTime LiveCutoff => DateTime.UtcNow - StaleAfter;

        public static bool IsLive(GameSession s) => s.EndedAt == null && s.LastHeartbeatAt > LiveCutoff;
    }
}
