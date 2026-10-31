using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class EventsTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public EventsTests(VanasFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private async Task<HttpClient> PlayerAsync(string name)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123"), Json)).EnsureSuccessStatusCode();
        return await LoginAsync(name, "Password-123");
    }

    private async Task<int> CreateEventAsync(HttpClient admin, int capacity, bool published = true, int hoursFromNow = 48)
    {
        var res = await admin.PostAsJsonAsync("/api/events",
            new SaveEventRequest("Tournoi test", "Rocket League", "Description", DateTime.UtcNow.AddHours(hoursFromNow), capacity, 500, published), Json);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<EventDetail>(Json))!.Id;
    }

    [Fact]
    public async Task Players_cannot_create_events()
    {
        var player = await PlayerAsync("evt_noadmin");
        var res = await player.PostAsJsonAsync("/api/events",
            new SaveEventRequest("Tournoi", "x", "", DateTime.UtcNow.AddDays(1), 10, 0, true), Json);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Unpublished_events_are_hidden_from_the_public()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        var id = await CreateEventAsync(admin, 4, published: false);

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/events/{id}")).StatusCode);
        var list = (await _factory.CreateClient().GetFromJsonAsync<List<EventSummary>>("/api/events", Json))!;
        Assert.DoesNotContain(list, e => e.Id == id);
    }

    [Fact]
    public async Task Registration_fills_up_and_refuses_extra_players()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        var id = await CreateEventAsync(admin, capacity: 1);
        var first = await PlayerAsync("evt_first");
        var second = await PlayerAsync("evt_second");

        var ok = await first.PostAsync($"/api/events/{id}/register", null);
        ok.EnsureSuccessStatusCode();
        var ticket = (await ok.Content.ReadFromJsonAsync<TicketInfo>(Json))!;
        Assert.Equal(10, ticket.TicketCode.Length);

        Assert.Equal(HttpStatusCode.Conflict, (await first.PostAsync($"/api/events/{id}/register", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsync($"/api/events/{id}/register", null)).StatusCode);

        var detail = (await _factory.CreateClient().GetFromJsonAsync<EventDetail>($"/api/events/{id}", Json))!;
        Assert.Equal(1, detail.RegisteredCount);

        // Freeing the seat lets the other player in.
        (await first.DeleteAsync($"/api/events/{id}/register")).EnsureSuccessStatusCode();
        (await second.PostAsync($"/api/events/{id}/register", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Registration_requires_login_and_a_future_event()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        var future = await CreateEventAsync(admin, 5);
        var past = await CreateEventAsync(admin, 5, hoursFromNow: -2);
        var player = await PlayerAsync("evt_timing");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsync($"/api/events/{future}/register", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsync($"/api/events/{past}/register", null)).StatusCode);
    }

    [Fact]
    public async Task Ticket_can_be_checked_in_only_once()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        var id = await CreateEventAsync(admin, 5);
        var player = await PlayerAsync("evt_door");
        var ticket = (await (await player.PostAsync($"/api/events/{id}/register", null)).Content.ReadFromJsonAsync<TicketInfo>(Json))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync("/api/events/check-in", new CheckInRequest(ticket.TicketCode), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/events/check-in", new CheckInRequest("ZZZZZZZZZZ"), Json)).StatusCode);

        var first = await admin.PostAsJsonAsync("/api/events/check-in", new CheckInRequest(ticket.TicketCode.ToLowerInvariant()), Json);
        first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/events/check-in", new CheckInRequest(ticket.TicketCode), Json)).StatusCode);

        var mine = (await player.GetFromJsonAsync<List<TicketInfo>>("/api/events/mine", Json))!;
        Assert.True(mine.Single(t => t.EventId == id).CheckedIn);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.DeleteAsync($"/api/events/{id}/register")).StatusCode);

        var regs = (await admin.GetFromJsonAsync<List<RegistrationInfo>>($"/api/events/manage/{id}/registrations", Json))!;
        Assert.Single(regs);
        Assert.NotNull(regs[0].CheckedInAt);
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_registered_players()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        var id = await CreateEventAsync(admin, 3);
        (await (await PlayerAsync("evt_cap1")).PostAsync($"/api/events/{id}/register", null)).EnsureSuccessStatusCode();
        (await (await PlayerAsync("evt_cap2")).PostAsync($"/api/events/{id}/register", null)).EnsureSuccessStatusCode();

        var res = await admin.PutAsJsonAsync($"/api/events/{id}",
            new SaveEventRequest("Tournoi test", "x", "", DateTime.UtcNow.AddDays(2), 1, 0, true), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
