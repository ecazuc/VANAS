using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Tests;

public class TariffsTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public TariffsTests(VanasFactory factory) => _factory = factory;

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

    private async Task<(int id, HttpClient client)> PlayerAsync(string name)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123", new DateOnly(1990, 1, 1)), Json)).EnsureSuccessStatusCode();
        var admin = await AdminAsync();
        var users = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!;
        return (users.Single(u => u.Username == name).Id, await LoginAsync(name, "Password-123"));
    }

    private async Task<HttpClient> StaffAsync(string name)
    {
        var (id, _) = await PlayerAsync(name);
        var admin = await AdminAsync();
        (await admin.PutAsJsonAsync($"/api/users/{id}/role", new SetRoleRequest(UserRole.Staff), Json)).EnsureSuccessStatusCode();
        return await LoginAsync(name, "Password-123");
    }

    private async Task<int> CreateOfferAsync(HttpClient admin, string name, OfferKind kind, int minutes = 0, int days = 0, int daily = 0, int price = 500, bool active = true)
    {
        var res = await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest(name, "", kind, minutes, days, daily, price, active, 0), Json);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<OfferDto>(Json))!.Id;
    }

    private Task<HttpResponseMessage> SellAsync(HttpClient staff, int userId, int offerId, PaymentMethod method = PaymentMethod.Cash) =>
        staff.PostAsJsonAsync("/api/sales", new SellRequest(userId, offerId, method, null), Json);

    private async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    private async Task<int> StartSessionAsync(HttpClient player, string station)
    {
        var res = await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest(station), Json);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SessionInfo>(Json))!.SessionId;
    }

    /// <summary>Pretends the PC last spoke <paramref name="seconds"/> ago, then sends a heartbeat that bills that gap.</summary>
    private async Task<HeartbeatResponse> HeartbeatAfterAsync(HttpClient player, int sessionId, int seconds)
    {
        await WithDbAsync(async db =>
        {
            var s = await db.Sessions.FindAsync(sessionId);
            s!.LastHeartbeatAt = DateTime.UtcNow.AddSeconds(-seconds);
            await db.SaveChangesAsync();
        });
        var res = await player.PostAsync($"/api/sessions/{sessionId}/heartbeat", null);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
    }

    [Fact]
    public async Task Only_admins_change_the_price_list_and_the_public_sees_active_offers_only()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff1");
        var (_, player) = await PlayerAsync("tar_player1");
        var body = new SaveOfferRequest("Test", "", OfferKind.HourPack, 60, 0, 0, 300, true, 0);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/offers", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync("/api/offers", body, Json)).StatusCode);

        var visible = await CreateOfferAsync(admin, "Visible 1h", OfferKind.HourPack, 60);
        var hidden = await CreateOfferAsync(admin, "Cachée 1h", OfferKind.HourPack, 60, active: false);

        var list = (await _factory.CreateClient().GetFromJsonAsync<List<OfferDto>>("/api/offers", Json))!;
        Assert.Contains(list, o => o.Id == visible);
        Assert.DoesNotContain(list, o => o.Id == hidden);
    }

    [Fact]
    public async Task Offer_validation_rejects_nonsense()
    {
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest("X", "", OfferKind.HourPack, 60, 0, 0, 100, true, 0), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest("Pack vide", "", OfferKind.HourPack, 0, 0, 0, 100, true, 0), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest("Pass sans durée", "", OfferKind.UnlimitedPass, 0, 0, 0, 100, true, 0), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/offers", new SaveOfferRequest("Prix négatif", "", OfferKind.HourPack, 60, 0, 0, -1, true, 0), Json)).StatusCode);
    }

    [Fact]
    public async Task Selling_an_hour_pack_credits_the_minutes_and_is_recorded_in_the_day_report()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff2");
        var (playerId, player) = await PlayerAsync("tar_player2");
        var pack = await CreateOfferAsync(admin, "5 heures", OfferKind.HourPack, 300, price: 1200);

        Assert.Equal(HttpStatusCode.Forbidden, (await SellAsync(player, playerId, pack)).StatusCode);

        var res = await SellAsync(staff, playerId, pack, PaymentMethod.Card);
        res.EnsureSuccessStatusCode();
        var sold = (await res.Content.ReadFromJsonAsync<SellResult>(Json))!;
        Assert.Equal(300, sold.MinutesLeft, 1);
        Assert.Equal(1200, sold.Sale.PriceCents);

        var profile = (await player.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!;
        Assert.Equal(300, profile.MinutesLeft, 1);

        var report = (await staff.GetFromJsonAsync<SalesReport>("/api/sales", Json))!;
        Assert.Contains(report.Items, s => s.UserName == "tar_player2" && s.PaymentMethod_IsCard());
        Assert.True(report.TotalCents >= 1200);
        Assert.Contains(report.ByMethod, m => m.Method == PaymentMethod.Card && m.TotalCents >= 1200);
    }

    [Fact]
    public async Task Inactive_or_unknown_offers_cannot_be_sold()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff3");
        var (playerId, _) = await PlayerAsync("tar_player3");
        var hidden = await CreateOfferAsync(admin, "Retirée", OfferKind.HourPack, 60, active: false);

        Assert.Equal(HttpStatusCode.NotFound, (await SellAsync(staff, playerId, hidden)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SellAsync(staff, playerId, 99999)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SellAsync(staff, 99999, hidden)).StatusCode);
    }

    [Fact]
    public async Task Unlimited_pass_pays_for_play_without_touching_the_hours_bought()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff4");
        var (playerId, player) = await PlayerAsync("tar_player4");
        var pass = await CreateOfferAsync(admin, "Pass mois", OfferKind.UnlimitedPass, days: 30, price: 4000);

        // Without anything bought, the player cannot start.
        Assert.Equal(HttpStatusCode.PaymentRequired, (await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-P4"), Json)).StatusCode);

        (await SellAsync(staff, playerId, pass)).EnsureSuccessStatusCode();
        var sessionId = await StartSessionAsync(player, "PC-P4");
        await HeartbeatAfterAsync(player, sessionId, 60);

        await WithDbAsync(async db =>
        {
            var s = await db.Sessions.FindAsync(sessionId);
            Assert.Equal(0, s!.MinutesConsumed, 3);
            var usage = db.PassUsage.Where(u => db.Subscriptions.Any(x => x.Id == u.SubscriptionId && x.UserId == playerId)).Sum(u => u.Minutes);
            Assert.InRange(usage, 0.99, 1.01);
        });

        var profile = (await player.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!;
        Assert.Contains("Pass mois", profile.PassSummary);
        Assert.InRange(profile.TotalMinutesPlayed, 0.99, 1.01);
    }

    [Fact]
    public async Task Daily_cap_hands_over_to_the_hours_bought()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff5");
        var (playerId, player) = await PlayerAsync("tar_player5");
        var pass = await CreateOfferAsync(admin, "Pass limité", OfferKind.UnlimitedPass, days: 7, daily: 30, price: 1500);
        var pack = await CreateOfferAsync(admin, "1 heure", OfferKind.HourPack, 60, price: 400);
        (await SellAsync(staff, playerId, pass)).EnsureSuccessStatusCode();
        (await SellAsync(staff, playerId, pack)).EnsureSuccessStatusCode();

        // 29.5 minutes already played today on the pass: only 0.5 minute of the cap is left.
        await WithDbAsync(async db =>
        {
            var sub = db.Subscriptions.Single(s => s.UserId == playerId);
            db.PassUsage.Add(new PassUsage { SubscriptionId = sub.Id, Minutes = 29.5, At = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        var sessionId = await StartSessionAsync(player, "PC-P5");
        await HeartbeatAfterAsync(player, sessionId, 60);

        await WithDbAsync(async db =>
        {
            var s = await db.Sessions.FindAsync(sessionId);
            Assert.InRange(s!.MinutesConsumed, 0.49, 0.51);
        });

        var live = (await player.GetFromJsonAsync<LiveStatus>("/api/profile/me/live", Json))!;
        Assert.InRange(live.MinutesLeft, 59.4, 59.6);
    }

    [Fact]
    public async Task Quota_pass_runs_out_after_its_minutes()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff6");
        var (playerId, player) = await PlayerAsync("tar_player6");
        var quota = await CreateOfferAsync(admin, "Quota 10 min", OfferKind.QuotaPass, minutes: 10, days: 30, price: 500);
        (await SellAsync(staff, playerId, quota)).EnsureSuccessStatusCode();

        await WithDbAsync(async db =>
        {
            var sub = db.Subscriptions.Single(s => s.UserId == playerId);
            db.PassUsage.Add(new PassUsage { SubscriptionId = sub.Id, Minutes = 9, At = DateTime.UtcNow.AddDays(-3) });
            await db.SaveChangesAsync();
        });

        var info = (await player.GetFromJsonAsync<List<SubscriptionInfo>>("/api/profile/me/subscriptions", Json))!;
        Assert.Equal(1, info.Single().MinutesLeft!.Value, 1);

        var sessionId = await StartSessionAsync(player, "PC-P6");
        var status = await HeartbeatAfterAsync(player, sessionId, 90);   // 1.5 min: 1 on the quota, 0.5 beyond it, nothing left

        Assert.Equal(SessionStatus.TimeExpired, status.Status);
    }

    [Fact]
    public async Task A_second_pass_of_the_same_kind_starts_when_the_first_ends()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff7");
        var (playerId, player) = await PlayerAsync("tar_player7");
        var pass = await CreateOfferAsync(admin, "Pass semaine", OfferKind.UnlimitedPass, days: 7, price: 1000);

        (await SellAsync(staff, playerId, pass)).EnsureSuccessStatusCode();
        (await SellAsync(staff, playerId, pass)).EnsureSuccessStatusCode();

        var passes = (await player.GetFromJsonAsync<List<SubscriptionInfo>>("/api/profile/me/subscriptions", Json))!;
        Assert.Equal(2, passes.Count);
        Assert.True(passes[0].IsActive);
        Assert.False(passes[1].IsActive);
        Assert.Equal(passes[0].EndsAt, passes[1].StartsAt);
        Assert.InRange((passes[1].EndsAt - passes[0].StartsAt).TotalDays, 13.99, 14.01);
    }

    private static Task<HttpResponseMessage> CancelAsync(HttpClient staff, int saleId, string reason = "Erreur de caisse") =>
        staff.PostAsJsonAsync($"/api/sales/{saleId}/cancel", new CancelSaleRequest(reason), Json);

    [Fact]
    public async Task Cancelling_an_hour_pack_takes_the_hours_back_and_leaves_the_totals()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff8");
        var (playerId, player) = await PlayerAsync("tar_player8");
        var pack = await CreateOfferAsync(admin, "2 heures annulables", OfferKind.HourPack, 120, price: 700);

        var sold = (await (await SellAsync(staff, playerId, pack, PaymentMethod.Cash)).Content.ReadFromJsonAsync<SellResult>(Json))!;
        Assert.Equal(120, sold.MinutesLeft, 1);
        var before = (await staff.GetFromJsonAsync<SalesReport>("/api/sales", Json))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await CancelAsync(player, sold.Sale.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CancelAsync(staff, sold.Sale.Id, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CancelAsync(staff, 99999)).StatusCode);

        (await CancelAsync(staff, sold.Sale.Id, "Mauvais joueur")).EnsureSuccessStatusCode();

        Assert.Equal(0, (await player.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!.MinutesLeft, 1);
        var after = (await staff.GetFromJsonAsync<SalesReport>("/api/sales", Json))!;
        Assert.Equal(before.TotalCents - 700, after.TotalCents);
        var row = Assert.Single(after.Items, s => s.Id == sold.Sale.Id);
        Assert.True(row.Cancelled);
        Assert.Equal("Mauvais joueur", row.CancelReason);

        // A sale can only be cancelled once.
        Assert.Equal(HttpStatusCode.Conflict, (await CancelAsync(staff, sold.Sale.Id)).StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_pass_ends_it_at_once()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync("tar_staff9");
        var (playerId, player) = await PlayerAsync("tar_player9");
        var pass = await CreateOfferAsync(admin, "Pass annulable", OfferKind.UnlimitedPass, days: 30, price: 4000);
        var sold = (await (await SellAsync(staff, playerId, pass)).Content.ReadFromJsonAsync<SellResult>(Json))!;
        Assert.NotNull((await player.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!.PassSummary);

        (await CancelAsync(staff, sold.Sale.Id)).EnsureSuccessStatusCode();

        Assert.Null((await player.GetFromJsonAsync<ProfileInfo>("/api/profile/me", Json))!.PassSummary);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await player.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-CANCEL"), Json)).StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_event_entry_makes_it_payable_again()
    {
        var admin = await AdminAsync();
        var created = await admin.PostAsJsonAsync("/api/events", new SaveEventRequest("Tournoi remboursé", "x", "", DateTime.UtcNow.AddDays(3), 10, 600, true), Json);
        var eventId = (await created.Content.ReadFromJsonAsync<EventDetail>(Json))!.Id;
        var (_, player) = await PlayerAsync("tar_player10");
        var ticket = (await (await player.PostAsync($"/api/events/{eventId}/register", null)).Content.ReadFromJsonAsync<TicketInfo>(Json))!;

        (await admin.PostAsJsonAsync("/api/events/check-in", new CheckInRequest(ticket.TicketCode, PaymentMethod.Card), Json)).EnsureSuccessStatusCode();
        var sale = (await admin.GetFromJsonAsync<SalesReport>("/api/sales", Json))!.Items.Single(s => s.UserName == "tar_player10");

        (await CancelAsync(admin, sale.Id)).EnsureSuccessStatusCode();

        var lookup = (await admin.GetFromJsonAsync<TicketLookup>($"/api/events/tickets/{ticket.TicketCode}", Json))!;
        Assert.Equal(600, lookup.AmountDueCents);
        Assert.False(lookup.CheckedIn);
        (await admin.PostAsJsonAsync("/api/events/check-in", new CheckInRequest(ticket.TicketCode, PaymentMethod.Cash), Json)).EnsureSuccessStatusCode();
    }
}

internal static class SaleDtoExtensions
{
    public static bool PaymentMethod_IsCard(this SaleDto s) => s.Method == PaymentMethod.Card;
}
