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

            new LoginWindow().Show();
        }

        /// <summary>Dev-only exit (Shift+F12). Replaced by an admin PIN in the kiosk step (PC-2).</summary>
        public static void HandleExitKey(KeyEventArgs e)
        {
            if (e.Key == Key.F12 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                Current.Shutdown();
        }
    }
}
