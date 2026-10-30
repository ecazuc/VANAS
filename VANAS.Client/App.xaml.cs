using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using Refit;
using VANAS.Client.Interfaces;
using VANAS.Client.Services;
using VANAS.Client.Views;

namespace VANAS.Client
{
    public partial class App : Application
    {
        public static ClientConfig Config { get; private set; } = new();
        public static IGamingApi Api { get; private set; } = null!;
        public static SessionManager Session { get; private set; } = null!;

        /// <summary>JWT of the logged-in customer, attached to API calls by <see cref="BearerTokenHandler"/>.</summary>
        public static string? Token { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Config = ClientConfig.Load();

            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter());

            var settings = new RefitSettings { ContentSerializer = new SystemTextJsonContentSerializer(json) };
            var http = new HttpClient(new BearerTokenHandler(() => Token)) { BaseAddress = new Uri(Config.ServerUrl), Timeout = TimeSpan.FromSeconds(8) };
            Api = RestService.For<IGamingApi>(http, settings);
            Session = new SessionManager(Api);
            Kiosk.Initialize(Config);

            new LoginWindow().Show();
        }

        /// <summary>True once the application is really shutting down (lets windows close despite kiosk protection).</summary>
        public static bool Exiting { get; private set; }

        /// <summary>When the current customer session began; used to close only what they started.</summary>
        public static DateTime SessionStartedAt { get; set; }

        /// <summary>
        /// Exit shortcut. Dev (no kiosk): Shift+F12. Kiosk: Ctrl+Shift+F12 then the admin PIN.
        /// </summary>
        public static void HandleExitKey(KeyEventArgs e, Window owner)
        {
            if (e.Key != Key.F12) return;
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (!Kiosk.Enabled && shift)
            {
                Quit();
            }
            else if (Kiosk.Enabled && shift && ctrl && !string.IsNullOrEmpty(Config.AdminPinHash))
            {
                if (new PinWindow { Owner = owner }.ShowDialog() == true) Quit();
            }
        }

        private static void Quit()
        {
            Exiting = true;
            Kiosk.Release();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Kiosk.Release();
            base.OnExit(e);
        }
    }
}
