using System.IO;
using System.Text.Json;

namespace VANAS.Client.Services
{
    /// <summary>Per-PC settings read from client.json next to the executable.</summary>
    public class ClientConfig
    {
        public string ServerUrl { get; set; } = "http://localhost:5049";
        public string StationName { get; set; } = string.Empty;

        public static ClientConfig Load()
        {
            var config = new ClientConfig();
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "client.json");
                if (File.Exists(path))
                    config = JsonSerializer.Deserialize<ClientConfig>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? config;
            }
            catch
            {
                // Unreadable config: fall back to defaults rather than blocking the PC.
            }

            if (string.IsNullOrWhiteSpace(config.StationName)) config.StationName = Environment.MachineName;
            return config;
        }
    }
}
