using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class CafeInfoTests : IClassFixture<VanasFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly VanasFactory _factory;

    public CafeInfoTests(VanasFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), Json);
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    [Fact]
    public async Task Everyone_can_read_the_cafe_info_but_only_admins_change_it()
    {
        var anon = _factory.CreateClient();
        var empty = (await anon.GetFromJsonAsync<CafeInfoDto>("/api/cafe", Json))!;
        Assert.Empty(empty.PhotoUrls);

        (await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("cafe_player", "Password-123", new DateOnly(1990, 1, 1)), Json)).EnsureSuccessStatusCode();
        var player = await LoginAsync("cafe_player", "Password-123");
        var body = new SaveCafeInfoRequest("Lun-Ven 14h-22h", "1 rue du Test", "01 02 03 04 05", "contact@cafe.test", "## Nos PC", "https://exemple.org/a.jpg\n https://exemple.org/b.jpg \n");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync("/api/cafe", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PutAsJsonAsync("/api/cafe", body, Json)).StatusCode);

        var admin = await LoginAsync("admin", "Admin-Test-123!");
        (await admin.PutAsJsonAsync("/api/cafe", body, Json)).EnsureSuccessStatusCode();

        var saved = (await anon.GetFromJsonAsync<CafeInfoDto>("/api/cafe", Json))!;
        Assert.Equal("Lun-Ven 14h-22h", saved.OpeningHours);
        Assert.Equal("1 rue du Test", saved.Address);
        Assert.Equal(2, saved.PhotoUrls.Count);
        Assert.Equal("https://exemple.org/b.jpg", saved.PhotoUrls[1]);
    }

    [Fact]
    public async Task Invalid_values_are_refused()
    {
        var admin = await LoginAsync("admin", "Admin-Test-123!");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/cafe", new SaveCafeInfoRequest("", "", "", "pas-un-mail", "", ""), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/cafe", new SaveCafeInfoRequest("", "", "", "", "", "javascript:alert(1)"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/cafe", new SaveCafeInfoRequest("", "", "", "", "", string.Join('\n', Enumerable.Range(0, 9).Select(i => $"https://exemple.org/{i}.jpg"))), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/cafe", new SaveCafeInfoRequest(new string('x', 1001), "", "", "", "", ""), Json)).StatusCode);
    }
}
