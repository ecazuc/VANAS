using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Launchers;
using VANAS.Shared.Models;

namespace VANAS.Tests;

public class AccountDataTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public AccountDataTests(VanasFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string username, string? password = null)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password ?? (username == "admin" ? "Admin-Test-123!" : "Password-123")), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private async Task<(int id, HttpClient client)> PlayerAsync(string name)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123", new DateOnly(1995, 5, 5)), Json)).EnsureSuccessStatusCode();
        var admin = await LoginAsync("admin");
        var id = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!.Single(u => u.Username == name).Id;
        return (id, await LoginAsync(name));
    }

    /// <summary>Gives a player a bit of everything: hours, a session, a purchase, a ticket, a game and a chat message.</summary>
    private async Task<int> FillAccountAsync(int id, HttpClient player, string station)
    {
        var admin = await LoginAsync("admin");
        var pack = (await (await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest("Pack données " + station, "", OfferKind.HourPack, 120, 0, 0, 900, true, 0), Json)).Content.ReadFromJsonAsync<OfferDto>(Json))!;
        (await admin.PostAsJsonAsync("/api/sales", new SellRequest(id, pack.Id, PaymentMethod.Card, "note privée"), Json)).EnsureSuccessStatusCode();
        (await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest(station), Json)).EnsureSuccessStatusCode();

        var ev = (await (await admin.PostAsJsonAsync("/api/events", new SaveEventRequest("Tournoi données " + station, "x", "", DateTime.UtcNow.AddDays(2), 5, 0, true), Json)).Content.ReadFromJsonAsync<EventDetail>(Json))!;
        (await player.PostAsync($"/api/events/{ev.Id}/register", null)).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync("/api/catalog", new SaveCatalogGameRequest("Jeu données " + station, GameLauncher.Steam, "data" + Guid.NewGuid().ToString("N")[..8], true), Json)).EnsureSuccessStatusCode();
        var game = (await admin.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog/all", Json))!.Single(g => g.Title == "Jeu données " + station).Id;
        (await player.PostAsync("/api/chat/rules/accept", null)).EnsureSuccessStatusCode();
        (await player.PutAsJsonAsync("/api/chat/games/mine", new SetGamesRequest(new List<int> { game }), Json)).EnsureSuccessStatusCode();
        (await player.PostAsJsonAsync($"/api/chat/rooms/{game}/messages", new SendMessageRequest("Bonjour à tous"), Json)).EnsureSuccessStatusCode();
        return ev.Id;
    }

    [Fact]
    public async Task A_player_can_download_a_copy_of_their_data()
    {
        var (id, player) = await PlayerAsync("data_export1");
        await FillAccountAsync(id, player, "PC-EXP1");

        var res = await player.GetAsync("/api/profile/me/export");
        res.EnsureSuccessStatusCode();
        var text = await res.Content.ReadAsStringAsync();

        Assert.Contains("data_export1", text);
        Assert.Contains("1995-05-05", text);          // birth date
        Assert.Contains("PC-EXP1", text);             // session
        Assert.Contains("Pack données PC-EXP1", text); // purchase
        Assert.Contains("Tournoi données PC-EXP1", text); // ticket
        Assert.Contains("Bonjour à tous", text);      // own message
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2", text);            // no bcrypt hash

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/profile/me/export")).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_needs_the_password_and_removes_the_personal_data()
    {
        var (id, player) = await PlayerAsync("data_delete1");
        var eventId = await FillAccountAsync(id, player, "PC-DEL1");

        Assert.Equal(HttpStatusCode.BadRequest, (await player.SendAsync(Delete("wrong-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.SendAsync(Delete(""))).StatusCode);
        (await player.GetAsync("/api/profile/me")).EnsureSuccessStatusCode();   // still there

        (await player.SendAsync(Delete("Password-123"))).EnsureSuccessStatusCode();

        // The account cannot be used any more and nothing personal is left, except the anonymised sale.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("data_delete1", "Password-123"), Json)).StatusCode);
        var admin = await LoginAsync("admin");
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!, u => u.Username == "data_delete1");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.Sessions.Where(s => s.UserId == id));
        Assert.Empty(db.TimeLedger.Where(t => t.UserId == id));
        Assert.Empty(db.ChatMessages.Where(m => m.SenderId == id));
        Assert.Empty(db.PlayerGames.Where(p => p.UserId == id));
        Assert.Empty(db.EventRegistrations.Where(r => r.UserId == id));

        var sale = Assert.Single(db.Sales.Where(s => s.UserId == id));
        Assert.Equal("(compte supprimé)", sale.UserName);
        Assert.Null(sale.Note);
        Assert.Equal(900, sale.PriceCents);   // the amount stays in the books

        // The seat taken at the event is free again.
        var detail = (await admin.GetFromJsonAsync<EventDetail>($"/api/events/manage/{eventId}", Json))!;
        Assert.Equal(0, detail.RegisteredCount);
    }

    [Fact]
    public async Task Staff_accounts_cannot_delete_themselves_through_the_player_route()
    {
        var admin = await LoginAsync("admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.SendAsync(Delete("Admin-Test-123!"))).StatusCode);
        (await admin.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage Delete(string password) =>
        new(HttpMethod.Delete, "/api/profile/me") { Content = JsonContent.Create(new DeleteAccountRequest(password), options: Json) };
}
