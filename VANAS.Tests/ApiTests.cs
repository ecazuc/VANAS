using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Tests;

public class VanasFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vanas-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-1234");
        builder.UseSetting("Seed:AdminUsername", "admin");
        builder.UseSetting("Seed:AdminPassword", "Admin-Test-123!");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* temp file */ }
    }
}

public class ApiTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public ApiTests(VanasFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => LoginAsync("admin", "Admin-Test-123!");

    private async Task<(int id, HttpClient client)> NewClientUserAsync(string name, double minutes)
    {
        var anon = _factory.CreateClient();
        (await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123"), Json)).EnsureSuccessStatusCode();

        var admin = await AdminAsync();
        var users = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!;
        var id = users.Single(u => u.Username == name).Id;
        if (minutes != 0)
            (await admin.PostAsJsonAsync($"/api/users/{id}/add-time", new AddTimeRequest(minutes, "test"), Json)).EnsureSuccessStatusCode();

        return (id, await LoginAsync(name, "Password-123"));
    }

    [Fact]
    public async Task Seeded_admin_can_login()
    {
        var admin = await AdminAsync();
        var me = await admin.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json);
        Assert.Equal(UserRole.Admin, me!.Role);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "nope"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/sessions/start", null)).StatusCode);
    }

    [Fact]
    public async Task Client_cannot_use_staff_endpoints()
    {
        var (id, client) = await NewClientUserAsync("cantadd", 0);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        var res = await client.PostAsJsonAsync($"/api/users/{id}/add-time", new AddTimeRequest(600, null), Json);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task User_list_never_exposes_password_hashes()
    {
        var admin = await AdminAsync();
        var raw = await admin.GetStringAsync("/api/users");
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Staff_adds_time_and_balance_is_updated()
    {
        var (id, client) = await NewClientUserAsync("buyer", 60);
        var me = await client.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json);
        Assert.Equal(60, me!.MinutesLeft, 3);
    }

    [Fact]
    public async Task Session_cannot_start_without_time()
    {
        var (_, client) = await NewClientUserAsync("broke", 0);
        var res = await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-01"), Json);
        Assert.Equal(HttpStatusCode.PaymentRequired, res.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_consumes_time_on_the_server_and_balance_survives_a_crash()
    {
        var (_, client) = await NewClientUserAsync("player", 10);
        var started = (await (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-02"), Json))
            .Content.ReadFromJsonAsync<SessionInfo>(Json))!;

        await Task.Delay(1500);
        var hb = (await (await client.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null))
            .Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
        Assert.Equal(SessionStatus.Active, hb.Status);
        Assert.InRange(hb.MinutesLeft, 9.9, 9.99);

        // "Crash": the client never calls /end. A fresh login still sees the consumed time.
        var relog = await LoginAsync("player", "Password-123");
        var me = await relog.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json);
        Assert.InRange(me!.MinutesLeft, 9.9, 9.99);
    }

    [Fact]
    public async Task Kick_ends_the_session_on_next_heartbeat()
    {
        var (id, client) = await NewClientUserAsync("kicked", 10);
        var started = (await (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-03"), Json))
            .Content.ReadFromJsonAsync<SessionInfo>(Json))!;

        var admin = await AdminAsync();
        (await admin.PostAsync($"/api/users/{id}/kick", null)).EnsureSuccessStatusCode();

        var hb = (await (await client.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null))
            .Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
        Assert.Equal(SessionStatus.Kicked, hb.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null)).StatusCode);
    }

    [Fact]
    public async Task Stations_endpoint_shows_who_is_playing_where_and_is_staff_only()
    {
        var (_, client) = await NewClientUserAsync("onstation", 10);
        (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-LIVE"), Json)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/stations")).StatusCode);

        var admin = await AdminAsync();
        var stations = (await admin.GetFromJsonAsync<List<StationStatus>>("/api/stations", Json))!;
        var pc = stations.Single(s => s.Name == "PC-LIVE");
        Assert.True(pc.IsInUse);
        Assert.Equal("onstation", pc.Username);
    }

    [Fact]
    public async Task Kick_and_added_time_are_pushed_instantly_to_the_players_pc()
    {
        var (id, client) = await NewClientUserAsync("pushed", 10);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter;

        var refreshes = System.Threading.Channels.Channel.CreateUnbounded<int>();
        var hub = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/session"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
        hub.On("Refresh", () => refreshes.Writer.TryWrite(1));
        await hub.StartAsync();

        var admin = await AdminAsync();
        (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-PUSH"), Json)).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/users/{id}/add-time", new AddTimeRequest(5, null), Json)).EnsureSuccessStatusCode();
        await refreshes.Reader.ReadAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        (await admin.PostAsync($"/api/users/{id}/kick", null)).EnsureSuccessStatusCode();
        await refreshes.Reader.ReadAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        await hub.DisposeAsync();
    }

    [Fact]
    public async Task Hub_rejects_connections_without_a_token()
    {
        var hub = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/session"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();
        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }

    [Fact]
    public async Task New_account_has_a_default_profile_and_can_change_pseudo_and_avatar()
    {
        var (_, client) = await NewClientUserAsync("profiled", 0);

        var profile = (await client.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!;
        Assert.Equal("profiled", profile.DisplayName);
        Assert.Equal(Avatars.Default, profile.AvatarKey);

        var res = await client.PutAsJsonAsync("/api/profile/me", new UpdateProfileRequest("Shadow Fox", "cyan"), Json);
        res.EnsureSuccessStatusCode();
        var updated = (await res.Content.ReadFromJsonAsync<ProfileInfo>(Json))!;
        Assert.Equal("Shadow Fox", updated.DisplayName);
        Assert.Equal("cyan", updated.AvatarKey);
    }

    [Theory]
    [InlineData("ab", "cyan")]            // too short
    [InlineData("bad<script>", "cyan")]   // forbidden characters
    [InlineData("Valid Name", "nope")]    // unknown avatar
    public async Task Invalid_profile_updates_are_rejected(string name, string avatar)
    {
        var (_, client) = await NewClientUserAsync("strict" + Math.Abs(name.GetHashCode() % 1000), 0);
        var res = await client.PutAsJsonAsync("/api/profile/me", new UpdateProfileRequest(name, avatar), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Pseudo_must_be_unique_ignoring_case()
    {
        var (_, first) = await NewClientUserAsync("uniq_a", 0);
        var (_, second) = await NewClientUserAsync("uniq_b", 0);

        (await first.PutAsJsonAsync("/api/profile/me", new UpdateProfileRequest("Ghost Rider", "red"), Json)).EnsureSuccessStatusCode();
        var clash = await second.PutAsJsonAsync("/api/profile/me", new UpdateProfileRequest("ghost rider", "red"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
    }

    [Fact]
    public async Task Profile_reports_time_played_and_session_history()
    {
        var (_, client) = await NewClientUserAsync("historian", 30);
        var started = (await (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-HIST"), Json))
            .Content.ReadFromJsonAsync<SessionInfo>(Json))!;
        await Task.Delay(1200);
        (await client.PostAsync($"/api/sessions/{started.SessionId}/end", null)).EnsureSuccessStatusCode();

        var profile = (await client.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!;
        Assert.Equal(1, profile.SessionCount);
        Assert.InRange(profile.TotalMinutesPlayed, 0.01, 0.2);

        var history = (await client.GetFromJsonAsync<List<SessionHistoryItem>>("/api/profile/me/sessions", Json))!;
        var item = Assert.Single(history);
        Assert.Equal("PC-HIST", item.StationName);
        Assert.Equal(SessionEndReason.UserLogout, item.EndReason);
    }

    [Fact]
    public async Task Dates_are_sent_as_UTC_with_a_Z_suffix()
    {
        var (_, client) = await NewClientUserAsync("utcuser", 10);
        (await client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-UTC"), Json)).EnsureSuccessStatusCode();
        var raw = await client.GetStringAsync("/api/profile/me/sessions");
        Assert.Matches("\"startedAt\":\"[^\"]+Z\"", raw);
    }

    [Fact]
    public async Task Players_see_only_enabled_catalog_games_and_the_starter_catalog_exists()
    {
        var (_, player) = await NewClientUserAsync("cataloguer", 0);
        var visible = (await player.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog", Json))!;
        Assert.Contains(visible, g => g.Title == "Counter-Strike 2" && g.ExternalId == "730");
        Assert.All(visible, g => Assert.True(g.IsEnabled));

        var admin = await AdminAsync();
        var created = (await (await admin.PostAsJsonAsync("/api/catalog",
            new SaveCatalogGameRequest("Hidden Gem", VANAS.Shared.Launchers.GameLauncher.Steam, "999001", false), Json))
            .Content.ReadFromJsonAsync<CatalogGameDto>(Json))!;

        var afterHidden = (await player.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog", Json))!;
        Assert.DoesNotContain(afterHidden, g => g.Id == created.Id);

        (await admin.PutAsJsonAsync($"/api/catalog/{created.Id}",
            new SaveCatalogGameRequest("Hidden Gem", VANAS.Shared.Launchers.GameLauncher.Steam, "999001", true), Json)).EnsureSuccessStatusCode();
        var afterEnabled = (await player.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog", Json))!;
        Assert.Contains(afterEnabled, g => g.Id == created.Id);

        (await admin.DeleteAsync($"/api/catalog/{created.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Only_staff_can_edit_the_catalog()
    {
        var (_, player) = await NewClientUserAsync("sneaky", 0);
        var res = await player.PostAsJsonAsync("/api/catalog",
            new SaveCatalogGameRequest("Cheat", VANAS.Shared.Launchers.GameLauncher.Steam, "1", true), Json);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync("/api/catalog/all")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/catalog")).StatusCode);
    }

    [Theory]
    [InlineData("730 & calc.exe")]
    [InlineData("a;b")]
    [InlineData("../../x")]
    [InlineData("")]
    [InlineData("with space")]
    public async Task Catalog_rejects_external_ids_that_could_inject_into_launcher_commands(string externalId)
    {
        var admin = await AdminAsync();
        var res = await admin.PostAsJsonAsync("/api/catalog",
            new SaveCatalogGameRequest("Evil", VANAS.Shared.Launchers.GameLauncher.Riot, externalId, true), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Duplicate_catalog_entries_are_rejected()
    {
        var admin = await AdminAsync();
        var dup = await admin.PostAsJsonAsync("/api/catalog",
            new SaveCatalogGameRequest("CS2 again", VANAS.Shared.Launchers.GameLauncher.Steam, "730", true), Json);
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Profile_endpoints_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/profile/me")).StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_change_roles_or_delete_users()
    {
        var admin = await AdminAsync();
        var (staffId, _) = await NewClientUserAsync("staffer", 0);
        (await admin.PutAsJsonAsync($"/api/users/{staffId}/role", new SetRoleRequest(UserRole.Staff), Json)).EnsureSuccessStatusCode();

        var staff = await LoginAsync("staffer", "Password-123");
        var (victim, _) = await NewClientUserAsync("victim", 0);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"/api/users/{victim}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.PutAsJsonAsync($"/api/users/{victim}/role", new SetRoleRequest(UserRole.Admin), Json)).StatusCode);
    }
}
