using System.IO;
using System.Text.Json;

namespace VANAS.Client.Services
{
    /// <summary>Per-PC settings read from client.json next to the executable.</summary>
    public class ClientConfig
    {
        public string ServerUrl { get; set; } = "http://localhost:5049";
        public string StationName { get; set; } = string.Empty;

        /// <summary>Turns on the lock-down behaviours; ignored unless Windows runs us under <see cref="KioskAccount"/>.</summary>
        public bool KioskMode { get; set; }
        public string KioskAccount { get; set; } = "vanas-client";

        /// <summary>Close games/launchers when the session ends. Keep true in production; set false to test the lock on a non-dedicated account.</summary>
        public bool CloseProgramsOnLogout { get; set; } = true;

        /// <summary>PBKDF2 hash of the admin PIN (generate with kiosk/New-AdminPin.ps1). Empty = no way to exit in kiosk mode.</summary>
        public string AdminPinHash { get; set; } = string.Empty;

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
