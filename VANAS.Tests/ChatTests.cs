using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using VANAS.Shared.Contracts;
using VANAS.Shared.Launchers;
using VANAS.Shared.Models;

namespace VANAS.Tests;

public class ChatTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public ChatTests(VanasFactory factory) => _factory = factory;

    private async Task<(string token, HttpClient client)> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return (body.Token, client);
    }

    private Task<(string token, HttpClient client)> AdminAsync() => LoginAsync("admin", "Admin-Test-123!");

    private record Player(int Id, string Name, string Token, HttpClient Client);

    private async Task<Player> NewPlayerAsync(string name, bool acceptRules = true)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, "Password-123", new DateOnly(1990, 1, 1)), Json)).EnsureSuccessStatusCode();
        var (_, admin) = await AdminAsync();
        var id = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/users", Json))!.Single(u => u.Username == name).Id;
        var (token, client) = await LoginAsync(name, "Password-123");
        if (acceptRules) (await client.PostAsync("/api/chat/rules/accept", null)).EnsureSuccessStatusCode();
        return new Player(id, name, token, client);
    }

    private async Task<Player> NewStaffAsync(string name)
    {
        var p = await NewPlayerAsync(name);
        var (_, admin) = await AdminAsync();
        (await admin.PutAsJsonAsync($"/api/users/{p.Id}/role", new SetRoleRequest(UserRole.Staff), Json)).EnsureSuccessStatusCode();
        var (token, client) = await LoginAsync(name, "Password-123");
        return p with { Token = token, Client = client };
    }

    private async Task<int> NewGameAsync(string title)
    {
        var (_, admin) = await AdminAsync();
        var res = await admin.PostAsJsonAsync("/api/catalog", new SaveCatalogGameRequest(title, GameLauncher.Steam, "chat" + Guid.NewGuid().ToString("N")[..8], true), Json);
        res.EnsureSuccessStatusCode();
        var games = (await admin.GetFromJsonAsync<List<CatalogGameDto>>("/api/catalog/all", Json))!;
        return games.Single(g => g.Title == title).Id;
    }

    private static Task<HttpResponseMessage> SetGamesAsync(Player p, params int[] ids) =>
        p.Client.PutAsJsonAsync("/api/chat/games/mine", new SetGamesRequest(ids.ToList()), Json);

    private static Task<HttpResponseMessage> SayInRoomAsync(Player p, int gameId, string text) =>
        p.Client.PostAsJsonAsync($"/api/chat/rooms/{gameId}/messages", new SendMessageRequest(text), Json);

    private static Task<HttpResponseMessage> SayDirectAsync(Player p, int toId, string text) =>
        p.Client.PostAsJsonAsync($"/api/chat/direct/{toId}/messages", new SendMessageRequest(text), Json);

    [Fact]
    public async Task Nothing_works_before_the_rules_are_accepted()
    {
        var p = await NewPlayerAsync("chat_norules", acceptRules: false);
        var game = await NewGameAsync("Jeu règles");
        (await SetGamesAsync(p, game)).EnsureSuccessStatusCode();

        Assert.False((await p.Client.GetFromJsonAsync<ChatStatus>("/api/chat/status", Json))!.RulesAccepted);
        Assert.Equal(HttpStatusCode.Forbidden, (await SayInRoomAsync(p, game, "salut")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.GetAsync("/api/chat/suggestions")).StatusCode);

        (await p.Client.PostAsync("/api/chat/rules/accept", null)).EnsureSuccessStatusCode();
        (await SayInRoomAsync(p, game, "salut")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Rooms_are_for_players_of_that_game_only()
    {
        var a = await NewPlayerAsync("chat_room_a");
        var b = await NewPlayerAsync("chat_room_b");
        var game = await NewGameAsync("Jeu salon");
        (await SetGamesAsync(a, game)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await SayInRoomAsync(b, game, "intrus")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync($"/api/chat/rooms/{game}/messages")).StatusCode);

        (await SayInRoomAsync(a, game, "Bonjour le salon")).EnsureSuccessStatusCode();
        (await SetGamesAsync(b, game)).EnsureSuccessStatusCode();
        var seen = (await b.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{game}/messages", Json))!;
        Assert.Contains(seen, m => m.Text == "Bonjour le salon" && m.SenderName == "chat_room_a");

        Assert.Equal(HttpStatusCode.BadRequest, (await SetGamesAsync(a, 99999)).StatusCode);
    }

    [Fact]
    public async Task Suggestions_list_players_who_share_a_game_and_opted_in()
    {
        var a = await NewPlayerAsync("chat_sug_a");
        var shared = await NewPlayerAsync("chat_sug_b");
        var other = await NewPlayerAsync("chat_sug_c");
        var silent = await NewPlayerAsync("chat_sug_d", acceptRules: false);
        var g1 = await NewGameAsync("Jeu A sug");
        var g2 = await NewGameAsync("Jeu B sug");
        await SetGamesAsync(a, g1, g2);
        await SetGamesAsync(shared, g1, g2);
        await SetGamesAsync(other, await NewGameAsync("Jeu C sug"));
        await SetGamesAsync(silent, g1);

        var list = (await a.Client.GetFromJsonAsync<List<PlayerSuggestion>>("/api/chat/suggestions", Json))!;
        var found = Assert.Single(list, s => s.UserId == shared.Id);
        Assert.Equal(2, found.SharedGames.Count);
        Assert.DoesNotContain(list, s => s.UserId == other.Id);
        Assert.DoesNotContain(list, s => s.UserId == silent.Id);
        Assert.DoesNotContain(list, s => s.UserId == a.Id);
    }

    [Fact]
    public async Task Private_messages_need_a_shared_game_and_arrive_in_both_conversations()
    {
        var a = await NewPlayerAsync("chat_dm_a");
        var b = await NewPlayerAsync("chat_dm_b");
        var stranger = await NewPlayerAsync("chat_dm_c");
        var game = await NewGameAsync("Jeu DM");
        await SetGamesAsync(a, game);
        await SetGamesAsync(b, game);

        Assert.Equal(HttpStatusCode.Forbidden, (await SayDirectAsync(stranger, a.Id, "coucou")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SayDirectAsync(a, a.Id, "moi")).StatusCode);

        (await SayDirectAsync(a, b.Id, "On joue ce soir ?")).EnsureSuccessStatusCode();

        var forB = (await b.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/direct/{a.Id}/messages", Json))!;
        Assert.Single(forB, m => m.Text == "On joue ce soir ?");
        var convs = (await b.Client.GetFromJsonAsync<List<ConversationDto>>("/api/chat/conversations", Json))!;
        Assert.Contains(convs, c => c.UserId == a.Id && c.LastText == "On joue ce soir ?");
        // Nobody else can read it.
        Assert.Empty((await stranger.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/direct/{a.Id}/messages", Json))!);

        // B can answer even if A later removes the shared game.
        await SetGamesAsync(a);
        (await SayDirectAsync(b, a.Id, "Oui !")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Blocking_cuts_the_conversation_in_both_directions()
    {
        var a = await NewPlayerAsync("chat_blk_a");
        var b = await NewPlayerAsync("chat_blk_b");
        var game = await NewGameAsync("Jeu block");
        await SetGamesAsync(a, game);
        await SetGamesAsync(b, game);
        (await SayDirectAsync(b, a.Id, "salut")).EnsureSuccessStatusCode();
        (await SayInRoomAsync(b, game, "message de B")).EnsureSuccessStatusCode();

        (await a.Client.PostAsync($"/api/chat/blocks/{b.Id}", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await SayDirectAsync(b, a.Id, "tu es là ?")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SayDirectAsync(a, b.Id, "non")).StatusCode);
        Assert.DoesNotContain((await a.Client.GetFromJsonAsync<List<PlayerSuggestion>>("/api/chat/suggestions", Json))!, s => s.UserId == b.Id);
        Assert.DoesNotContain((await b.Client.GetFromJsonAsync<List<PlayerSuggestion>>("/api/chat/suggestions", Json))!, s => s.UserId == a.Id);
        Assert.DoesNotContain((await a.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{game}/messages", Json))!, m => m.SenderId == b.Id);
        Assert.Empty((await a.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/direct/{b.Id}/messages", Json))!);

        (await a.Client.DeleteAsync($"/api/chat/blocks/{b.Id}")).EnsureSuccessStatusCode();
        (await SayDirectAsync(b, a.Id, "ça va mieux ?")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Links_phone_numbers_and_spam_are_refused()
    {
        var a = await NewPlayerAsync("chat_rules_a");
        var game = await NewGameAsync("Jeu filtre");
        await SetGamesAsync(a, game);

        Assert.Equal(HttpStatusCode.BadRequest, (await SayInRoomAsync(a, game, "regarde https://exemple.com")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SayInRoomAsync(a, game, "appelle le 06 12 34 56 78")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SayInRoomAsync(a, game, "   ")).StatusCode);

        // Rate limit: the 7th message within 10 seconds is refused.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++) statuses.Add((await SayInRoomAsync(a, game, $"message {i}")).StatusCode);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Reports_reach_staff_who_can_hide_mute_and_ban()
    {
        var staff = await NewStaffAsync("chat_mod_staff");
        var bad = await NewPlayerAsync("chat_mod_bad");
        var witness = await NewPlayerAsync("chat_mod_witness");
        var outsider = await NewPlayerAsync("chat_mod_outsider");
        var game = await NewGameAsync("Jeu modération");
        await SetGamesAsync(bad, game);
        await SetGamesAsync(witness, game);

        var msg = await (await SayInRoomAsync(bad, game, "message limite")).Content.ReadFromJsonAsync<ChatMessageDto>(Json);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync($"/api/chat/messages/{msg!.Id}/report", new ReportRequest("x"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bad.Client.PostAsJsonAsync($"/api/chat/messages/{msg.Id}/report", new ReportRequest("moi"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await witness.Client.PostAsJsonAsync($"/api/chat/messages/{msg.Id}/report", new ReportRequest(" "), Json)).StatusCode);
        (await witness.Client.PostAsJsonAsync($"/api/chat/messages/{msg.Id}/report", new ReportRequest("Insultes"), Json)).EnsureSuccessStatusCode();
        (await witness.Client.PostAsJsonAsync($"/api/chat/messages/{msg.Id}/report", new ReportRequest("Insultes"), Json)).EnsureSuccessStatusCode(); // no duplicate

        Assert.Equal(HttpStatusCode.Forbidden, (await witness.Client.GetAsync("/api/chat/moderation/reports")).StatusCode);
        var reports = (await staff.Client.GetFromJsonAsync<List<ReportDto>>("/api/chat/moderation/reports", Json))!;
        var report = Assert.Single(reports, r => r.MessageId == msg.Id);
        Assert.Equal("chat_mod_bad", report.SenderName);
        Assert.Equal("chat_mod_witness", report.ReporterName);

        // Mute for 24 hours: the message disappears and the player cannot write any more.
        (await staff.Client.PostAsJsonAsync($"/api/chat/moderation/reports/{report.Id}/resolve", new ModerateRequest(ModerationAction.Mute24Hours), Json)).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await witness.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{game}/messages", Json))!, m => m.Id == msg.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await SayInRoomAsync(bad, game, "encore moi")).StatusCode);
        Assert.NotNull((await bad.Client.GetFromJsonAsync<ChatStatus>("/api/chat/status", Json))!.MutedUntil);
        Assert.Contains((await staff.Client.GetFromJsonAsync<List<SanctionDto>>("/api/chat/moderation/sanctions", Json))!, s => s.UserId == bad.Id);

        // Lifting the sanction gives the voice back; a ban removes the whole chat.
        (await staff.Client.DeleteAsync($"/api/chat/moderation/sanctions/{bad.Id}")).EnsureSuccessStatusCode();
        (await SayInRoomAsync(bad, game, "pardon")).EnsureSuccessStatusCode();

        var again = await (await SayInRoomAsync(bad, game, "récidive")).Content.ReadFromJsonAsync<ChatMessageDto>(Json);
        (await witness.Client.PostAsJsonAsync($"/api/chat/messages/{again!.Id}/report", new ReportRequest("Encore"), Json)).EnsureSuccessStatusCode();
        var second = (await staff.Client.GetFromJsonAsync<List<ReportDto>>("/api/chat/moderation/reports", Json))!.Single(r => r.MessageId == again.Id);
        (await staff.Client.PostAsJsonAsync($"/api/chat/moderation/reports/{second.Id}/resolve", new ModerateRequest(ModerationAction.Ban), Json)).EnsureSuccessStatusCode();
        Assert.True((await bad.Client.GetFromJsonAsync<ChatStatus>("/api/chat/status", Json))!.IsBanned);
        Assert.Equal(HttpStatusCode.Forbidden, (await bad.Client.GetAsync($"/api/chat/rooms/{game}/messages")).StatusCode);
        Assert.DoesNotContain((await witness.Client.GetFromJsonAsync<List<PlayerSuggestion>>("/api/chat/suggestions", Json))!, s => s.UserId == bad.Id);
    }

    [Fact]
    public async Task Dismissing_a_report_keeps_the_message()
    {
        var staff = await NewStaffAsync("chat_dis_staff");
        var a = await NewPlayerAsync("chat_dis_a");
        var b = await NewPlayerAsync("chat_dis_b");
        var game = await NewGameAsync("Jeu classé");
        await SetGamesAsync(a, game);
        await SetGamesAsync(b, game);
        var msg = await (await SayInRoomAsync(a, game, "tout va bien")).Content.ReadFromJsonAsync<ChatMessageDto>(Json);
        (await b.Client.PostAsJsonAsync($"/api/chat/messages/{msg!.Id}/report", new ReportRequest("Erreur"), Json)).EnsureSuccessStatusCode();
        var report = (await staff.Client.GetFromJsonAsync<List<ReportDto>>("/api/chat/moderation/reports", Json))!.Single(r => r.MessageId == msg.Id);

        (await staff.Client.PostAsJsonAsync($"/api/chat/moderation/reports/{report.Id}/resolve", new ModerateRequest(ModerationAction.Dismiss), Json)).EnsureSuccessStatusCode();

        Assert.Contains((await b.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{game}/messages", Json))!, m => m.Id == msg.Id);
        Assert.DoesNotContain((await staff.Client.GetFromJsonAsync<List<ReportDto>>("/api/chat/moderation/reports", Json))!, r => r.Id == report.Id);
        (await SayInRoomAsync(a, game, "toujours là")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Room_messages_are_pushed_live_to_members_only()
    {
        var a = await NewPlayerAsync("chat_live_a");
        var b = await NewPlayerAsync("chat_live_b");
        var outsider = await NewPlayerAsync("chat_live_c");
        var game = await NewGameAsync("Jeu direct");
        await SetGamesAsync(a, game);
        await SetGamesAsync(b, game);

        async Task<HubConnection> ConnectAsync(Player p)
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

        await using var hubB = await ConnectAsync(b);
        await using var hubOut = await ConnectAsync(outsider);
        var received = new TaskCompletionSource<ChatMessageDto>();
        var leaked = false;
        hubB.On<ChatMessageDto>("Message", m => received.TrySetResult(m));
        hubOut.On<ChatMessageDto>("Message", _ => leaked = true);

        Assert.True(await hubB.InvokeAsync<bool>("JoinRoom", game));
        Assert.False(await hubOut.InvokeAsync<bool>("JoinRoom", game));

        (await SayInRoomAsync(a, game, "en direct !")).EnsureSuccessStatusCode();
        var got = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("en direct !", got.Text);
        Assert.False(leaked);
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_chat_data()
    {
        var a = await NewPlayerAsync("chat_del_a");
        var b = await NewPlayerAsync("chat_del_b");
        var game = await NewGameAsync("Jeu suppression");
        await SetGamesAsync(a, game);
        await SetGamesAsync(b, game);
        (await SayInRoomAsync(a, game, "je pars")).EnsureSuccessStatusCode();
        (await a.Client.PostAsync($"/api/chat/blocks/{b.Id}", null)).EnsureSuccessStatusCode();

        var (_, admin) = await AdminAsync();
        (await admin.DeleteAsync($"/api/users/{a.Id}")).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await b.Client.GetFromJsonAsync<List<ChatMessageDto>>($"/api/chat/rooms/{game}/messages", Json))!, m => m.SenderId == a.Id);
    }
}
