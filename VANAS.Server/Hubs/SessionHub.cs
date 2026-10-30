using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VANAS.Server.Hubs
{
    /// <summary>
    /// Push channel to the customer's PC. The server only says "Refresh": the client then
    /// syncs through the regular heartbeat, which stays the single source of truth.
    /// </summary>
    [Authorize]
    public class SessionHub : Hub
    {
        public const string Path = "/hubs/session";
        public const string RefreshMethod = "Refresh";
    }
}
