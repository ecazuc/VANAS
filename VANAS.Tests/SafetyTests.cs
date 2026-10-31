using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using VANAS.Server.Data;
using VANAS.Server.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Launchers;
using VANAS.Shared.Models;

namespace VANAS.Tests;

/// <summary>Minimum age, parental agreement, chat feed cut-off and message retention.</summary>
public class SafetyTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static DateOnly YearsAgo(int years, int extraDays = 0) =>
        DateOnly.FromDateTime(DateTime.Now).AddYears(-years).AddDays(-extraDays);

    private readonly VanasFactory _factory;

    public SafetyTests(VanasFactory factory) => _factory = factory;

    private async Task<(string token, HttpClient client)> LoginAsync(string username)
    {
        var password = username == "admin" ? "Admin-Test-123!" : "Password-123";
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return (body.Token, client);
    }

    private record Player(int Id, string Name, string Token, HttpClient Client);

    private Task<HttpResponseMessage> RegisterAsync(string name, DateOnly? birth) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123", birth), Json);

    private async Task<Player> NewPlayerAsync(string name, DateOnly birth, double minutes = 60)
    {
        (await RegisterAsync(name, birth)).EnsureSuccessStatusCode();
        var (_, admin) = await LoginAsync("admin");
        var id = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!.Single(u => u.Username == name).Id;
        if (minutes > 0) (await admin.PostAsJsonAsync($"/api/users/{id}/add-time", new AddTimeRequest(minutes, "test"), Json)).EnsureSuccessStatusCode();
        var (token, client) = await LoginAsync(name);
        return new Player(id, name, token, client);
    }

    private async Task<Player> NewStaffAsync(string name)
    {
        var p = await NewPlayerAsync(name, YearsAgo(30), minutes: 0);
        var (_, admin) = await LoginAsync("admin");
        (await admin.PutAsJsonAsync($"/api/users/{p.Id}/role", new SetRoleRequest(UserRole.Staff), Json)).EnsureSuccessStatusCode();
        var (token, client) = await LoginAsync(name);
        return p with { Token = token, Client = client };
    }

    private async Task<int> NewGameAsync(string title)
    {
        var (_, admin) = await LoginAsync("admin");
        (await admin.PostAsJsonAsync("/api/catalog", new SaveCatalogGameRequest(title, GameLauncher.Steam, "safe" + Guid.NewGuid().ToString("N")[..8], true), Json)).EnsureSuccessStatusCode();
        return (await admin.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog/all", Json))!.Single(g => g.Title == title).Id;
    }

    private static async Task JoinChatAsync(Player p, params int[] games)
    {
        (await p.Client.PostAsync("/api/chat/rules/accept", null)).EnsureSuccessStatusCode();
        (await p.Client.PutAsJsonAsync("/api/chat/games/mine", new SetGamesRequest(games.ToList()), Json)).EnsureSuccessStatusCode();
    }

    // ---------- Minimum age ----------

    [Fact]
    public async Task Sign_up_needs_a_valid_birth_date_and_the_minimum_age()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync("age_none", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync("age_future", DateOnly.FromDateTime(DateTime.Now).AddDays(5))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegisterAsync("age_ancient", YearsAgo(150))).StatusCode);

        var tooYoung = await RegisterAsync("age_eleven", YearsAgo(12).AddDays(1));   // turns 12 tomorrow
        Assert.Equal(HttpStatusCode.BadRequest, tooYoung.StatusCode);
        Assert.Contains("12 ans", await tooYoung.Content.ReadAsStringAsync());

        (await RegisterAsync("age_twelve", YearsAgo(12))).EnsureSuccessStatusCode();   // turned 12 today
    }

    // ---------- Parental agreement ----------

    [Fact]
    public async Task A_minor_cannot_play_chat_or_register_for_events_until_a_parent_agrees_at_the_counter()
    {
        var staff = await NewStaffAsync("safe_staff1");
        var kidRes = await RegisterAsync("safe_kid1", YearsAgo(14));
        kidRes.EnsureSuccessStatusCode();
        Assert.True((await kidRes.Content.ReadFromJsonAsync<RegisterResult>(Json))!.ConsentPending);

        var kid = await NewPlayerAsync("safe_kid1b", YearsAgo(14));
        var login = (await kid.Client.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json))!;
        Assert.True(login.ConsentPending);

        var session = await kid.Client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-KID"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, session.StatusCode);
        Assert.Contains("Accord parental", await session.Content.ReadAsStringAsync());

        var status = (await kid.Client.GetFromJsonAsync<ChatStatus>("/api/chat/status", Json))!;
        Assert.True(status.ConsentPending);
        (await kid.Client.PostAsync("/api/chat/rules/accept", null)).EnsureSuccessStatusCode();   // reading the rules is fine
        Assert.Equal(HttpStatusCode.Forbidden, (await kid.Client.GetAsync("/api/chat/suggestions")).StatusCode);

        // Only staff can record the agreement.
        Assert.Equal(HttpStatusCode.Forbidden, (await kid.Client.PostAsync($"/api/users/{kid.Id}/parental-consent", null)).StatusCode);
        var list = (await staff.Client.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!;
        var row = list.Single(u => u.Id == kid.Id);
        Assert.True(row.ConsentPending);
        Assert.Equal(14, row.Age);

        (await staff.Client.PostAsync($"/api/users/{kid.Id}/parental-consent", null)).EnsureSuccessStatusCode();

        Assert.False((await kid.Client.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json))!.ConsentPending);
        (await kid.Client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-KID"), Json)).EnsureSuccessStatusCode();
        Assert.False((await kid.Client.GetFromJsonAsync<ChatStatus>("/api/chat/status", Json))!.ConsentPending);
    }

    [Fact]
    public async Task Adults_need_no_agreement()
    {
        var adult = await NewPlayerAsync("safe_adult", YearsAgo(18));   // turned 18 today
        Assert.False((await adult.Client.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json))!.ConsentPending);
        (await adult.Client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-ADULT"), Json)).EnsureSuccessStatusCode();

        var almost = await NewPlayerAsync("safe_almost", YearsAgo(18).AddDays(1));   // turns 18 tomorrow
        Assert.True((await almost.Client.GetFromJsonAsync<LoginResponse>("/api/auth/me", Json))!.ConsentPending);
    }

    [Fact]
    public async Task Withdrawing_the_agreement_ends_the_session_and_blocks_the_account_again()
    {
        var staff = await NewStaffAsync("safe_staff2");
        var kid = await NewPlayerAsync("safe_kid2", YearsAgo(13));
        (await staff.Client.PostAsync($"/api/users/{kid.Id}/parental-consent", null)).EnsureSuccessStatusCode();
        var started = (await (await kid.Client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-K2"), Json)).Content.ReadFromJsonAsync<SessionInfo>(Json))!;

        (await staff.Client.DeleteAsync($"/api/users/{kid.Id}/parental-consent")).EnsureSuccessStatusCode();

        var hb = (await (await kid.Client.PostAsync($"/api/sessions/{started.SessionId}/heartbeat", null)).Content.ReadFromJsonAsync<HeartbeatResponse>(Json))!;
        Assert.Equal(SessionStatus.Kicked, hb.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await kid.Client.PostAsJsonAsync("/api/sessions/start", new StartSessionRequest("PC-K2"), Json)).StatusCode);
    }

    [Fact]
    public async Task Minors_with_an_agreement_and_adults_can_write_to_each_other()
    {
        var staff = await NewStaffAsync("safe_staff3");
        var kid = await NewPlayerAsync("safe_kid3", YearsAgo(15));
        var adult = await NewPlayerAsync("safe_adult3", YearsAgo(30));
        (await staff.Client.PostAsync($"/api/users/{kid.Id}/parental-consent", null)).EnsureSuccessStatusCode();
        var game = await NewGameAsync("Jeu âges");
        await JoinChatAsync(kid, game);
        await JoinChatAsync(adult, game);

        var res = await adult.Client.PostAsJsonAsync($"/api/chat/direct/{kid.Id}/messages", new SendMessageRequest("Salut, on joue ?"), Json);
        res.EnsureSuccessStatusCode();
    }

    // ---------- Chat feed cut-off ----------

    private async Task<HubConnection> ConnectAsync(Player p)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/chat"), o =>
            {
                o.AccessTokenProvider = () => Task.FromResult<string?>(p.Token);
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            }).Build();
        await hub.StartAsync();
        return hub;
    }

    [Fact]
    public async Task A_ban_and_a_removed_game_stop_the_live_room_feed_immediately()
    {
        var staff = await NewStaffAsync("safe_staff4");
        var talker = await NewPlayerAsync("safe_talker", YearsAgo(25), minutes: 0);
        var listener = await NewPlayerAsync("safe_listener", YearsAgo(25), minutes: 0);
        var game = await NewGameAsync("Jeu coupure");
        await JoinChatAsync(talker, game);
        await JoinChatAsync(listener, game);

        await using var hub = await ConnectAsync(listener);
        var received = new List<string>();
        hub.On<ChatMessageDto>("Message", m => { lock (received) received.Add(m.Text); });
        Assert.True(await hub.InvokeAsync<bool>("JoinRoom", game));

        async Task SayAsync(string text) => (await talker.Client.PostAsJsonAsync($"/api/chat/rooms/{game}/messages", new SendMessageRequest(text), Json)).EnsureSuccessStatusCode();
        async Task<bool> SeenAsync(string text)
        {
            for (var i = 0; i < 20; i++) { lock (received) if (received.Contains(text)) return true; await Task.Delay(100); }
            return false;
        }

        await SayAsync("premier");
        Assert.True(await SeenAsync("premier"));

        // Removing the game from my list stops the feed at once.
        (await listener.Client.PutAsJsonAsync("/api/chat/games/mine", new SetGamesRequest(new List<int>()), Json)).EnsureSuccessStatusCode();
        await SayAsync("deuxieme");
        Assert.False(await SeenAsync("deuxieme"));

        // Back in the room, then banned: the feed stops again, without any reconnection.
        (await listener.Client.PutAsJsonAsync("/api/chat/games/mine", new SetGamesRequest(new List<int> { game }), Json)).EnsureSuccessStatusCode();
        Assert.True(await hub.InvokeAsync<bool>("JoinRoom", game));
        await SayAsync("troisieme");
        Assert.True(await SeenAsync("troisieme"));

        var spam = await (await listener.Client.PostAsJsonAsync($"/api/chat/rooms/{game}/messages", new SendMessageRequest("message à signaler"), Json)).Content.ReadFromJsonAsync<ChatMessageDto>(Json);
        (await talker.Client.PostAsJsonAsync($"/api/chat/messages/{spam!.Id}/report", new ReportRequest("test"), Json)).EnsureSuccessStatusCode();
        var report = (await staff.Client.GetFromJsonAsync<List<ReportDto>>("/api/chat/moderation/reports", Json))!.Single(r => r.MessageId == spam.Id);
        (await staff.Client.PostAsJsonAsync($"/api/chat/moderation/reports/{report.Id}/resolve", new ModerateRequest(ModerationAction.Ban), Json)).EnsureSuccessStatusCode();

        await Task.Delay(1100); // rate limit window is per sender: wait so the next message is accepted
        await SayAsync("quatrieme");
        Assert.False(await SeenAsync("quatrieme"));
    }

    // ---------- Retention ----------

    [Fact]
    public async Task Old_messages_are_purged_but_reported_ones_are_kept_as_evidence_for_longer()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        ChatMessage Msg(string text, int daysOld) => new() { GameId = 1, SenderId = 1, Text = text, CreatedAt = now.AddDays(-daysOld) };
        var recent = Msg("recent", 10);
        var old = Msg("old", 120);
        var oldOpen = Msg("old, report still open", 120);
        var oldSettledRecently = Msg("old, settled 30 days ago", 200);
        var oldSettledLongAgo = Msg("old, settled 400 days ago", 500);
        db.ChatMessages.AddRange(recent, old, oldOpen, oldSettledRecently, oldSettledLongAgo);
        await db.SaveChangesAsync();

        db.MessageReports.AddRange(
            new MessageReport { MessageId = oldOpen.Id, ReporterId = 2, Reason = "x", CreatedAt = now.AddDays(-100) },
            new MessageReport { MessageId = oldSettledRecently.Id, ReporterId = 2, Reason = "x", CreatedAt = now.AddDays(-60), ResolvedAt = now.AddDays(-30) },
            new MessageReport { MessageId = oldSettledLongAgo.Id, ReporterId = 2, Reason = "x", CreatedAt = now.AddDays(-450), ResolvedAt = now.AddDays(-400) });
        await db.SaveChangesAsync();

        var deleted = await ChatRetentionService.PurgeAsync(db, now, messageDays: 90, reportDays: 365);

        var left = db.ChatMessages.Select(m => m.Text).ToList();
        Assert.Contains("recent", left);
        Assert.DoesNotContain("old", left);
        Assert.Contains("old, report still open", left);
        Assert.Contains("old, settled 30 days ago", left);
        Assert.DoesNotContain("old, settled 400 days ago", left);
        Assert.Equal(2, db.MessageReports.Count(r => r.MessageId == oldOpen.Id || r.MessageId == oldSettledRecently.Id));
        Assert.False(db.MessageReports.Any(r => r.MessageId == oldSettledLongAgo.Id));
        Assert.Equal(3, deleted);   // the plain old message, the long-settled message and its report
    }
}
