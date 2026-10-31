using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Tests;

public class StationMessageTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public StationMessageTests(VanasFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, username == "admin" ? "Admin-Test-123!" : "Password-123"), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private async Task<HttpClient> PlayerWithTimeAsync(string name)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123", new DateOnly(1990, 1, 1)), Json)).EnsureSuccessStatusCode();
        var admin = await LoginAsync("admin");
        var id = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!.Single(u => u.Username == name).Id;
        (await admin.PostAsJsonAsync($"/api/users/{id}/add-time", new AddTimeRequest(60, "test"), Json)).EnsureSuccessStatusCode();
        return await LoginAsync(name);
    }

    private static async Task<int> StationIdAsync(HttpClient admin, string name) =>
        (await admin.GetFromJsonAsync<List<StationStatus>>("/api/stations", Json))!.Single(s => s.Name == name).StationId;

    [Fact]
    public async Task Staff_message_reaches_the_player_once_with_the_next_heartbeat()
    {
        var admin = await LoginAsync("admin");
        var player = await PlayerWithTimeAsync("msg_player1");
        var started = (await (await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-MSG1"), Json)).Content.ReadFromJsonAsync<SessionInfo>(Json))!;
        var stationId = await StationIdAsync(admin, "PC-MSG1");

        (await admin.PostAsJsonAsync($"/api/stations/{stationId}/message", new StationMessageRequest("  Fermeture dans 10 minutes  "), Json)).EnsureSuccessStatusCode();

        var first = (await (await player.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null)).Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
        Assert.Equal("Fermeture dans 10 minutes", first.StaffMessage);
        var second = (await (await player.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null)).Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
        Assert.Null(second.StaffMessage);
    }

    [Fact]
    public async Task Messages_need_staff_rights_a_used_station_and_a_sensible_text()
    {
        var admin = await LoginAsync("admin");
        var player = await PlayerWithTimeAsync("msg_player2");
        await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-MSG2"), Json);
        var stationId = await StationIdAsync(admin, "PC-MSG2");

        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync($"/api/stations/{stationId}/message", new StationMessageRequest("hello"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/stations/{stationId}/message", new StationMessageRequest("   "), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/stations/{stationId}/message", new StationMessageRequest(new string('x', 201)), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/stations/99999/message", new StationMessageRequest("hello"), Json)).StatusCode);

        // A PC nobody is using cannot receive a message.
        var idle = await PlayerWithTimeAsync("msg_player3");
        var session = (await (await idle.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-MSG3"), Json)).Content.ReadFromJsonAsync<SessionInfo>(Json))!;
        var idleStation = await StationIdAsync(admin, "PC-MSG3");
        (await idle.PostAsync($"/api/sessions/{session.SessionId}/end", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/stations/{idleStation}/message", new StationMessageRequest("hello"), Json)).StatusCode);
    }
}
