namespace VANAS.Server.Hubs
{
    /// <summary>
    /// Which chat connections are listening to which room, so a ban or a removed game can cut the
    /// live feed right away instead of waiting for the player's app to disconnect.
    /// </summary>
    public class ChatPresence
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, (int UserId, HashSet<int> Rooms)> _connections = new();

        public void Joined(string connectionId, int userId, int gameId)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(connectionId, out var entry))
                    _connections[connectionId] = entry = (userId, new HashSet<int>());
                entry.Rooms.Add(gameId);
            }
        }

        public void Left(string connectionId, int gameId)
        {
            lock (_lock)
            {
                if (_connections.TryGetValue(connectionId, out var entry)) entry.Rooms.Remove(gameId);
            }
        }

        public void Disconnected(string connectionId)
        {
            lock (_lock) _connections.Remove(connectionId);
        }

        /// <summary>Forgets and returns the rooms a player listens to (one room, or all of them when gameId is null).</summary>
        public List<(string ConnectionId, int GameId)> Remove(int userId, int? gameId)
        {
            var removed = new List<(string, int)>();
            lock (_lock)
            {
                foreach (var (connectionId, entry) in _connections.Where(c => c.Value.UserId == userId).ToList())
                {
                    foreach (var room in entry.Rooms.Where(r => gameId == null || r == gameId).ToList())
                    {
                        entry.Rooms.Remove(room);
                        removed.Add((connectionId, room));
                    }
                }
            }
            return removed;
        }
    }
}
