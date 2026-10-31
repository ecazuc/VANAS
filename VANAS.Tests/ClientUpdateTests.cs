using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using VANAS.Shared.Contracts;

namespace VANAS.Tests;

/// <summary>A server whose update folder points to a temporary directory we control.</summary>
public class UpdateFactory : WebApplicationFactory<Program>
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "vanas-updates-" + Guid.NewGuid().ToString("N"));
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"vanas-test-{Guid.NewGuid():N}.db");

    public UpdateFactory() => Directory.CreateDirectory(Folder);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Folder);
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_db}");
        builder.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-1234");
        builder.UseSetting("Seed:AdminUsername", "admin");
        builder.UseSetting("Seed:AdminPassword", "Admin-Test-123!");
        builder.UseSetting("Client:UpdateFolder", Folder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); Directory.Delete(Folder, true); } catch { /* temp files */ }
    }
}

public class ClientUpdateTests : IClassFixture<UpdateFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UpdateFactory _factory;

    public ClientUpdateTests(UpdateFactory factory) => _factory = factory;

    private void Publish(string version, byte[] zip, string? hash = null)
    {
        File.WriteAllBytes(Path.Combine(_factory.Folder, "client.zip"), zip);
        File.WriteAllText(Path.Combine(_factory.Folder, "version.txt"), version);
        if (hash != null) File.WriteAllText(Path.Combine(_factory.Folder, "client.sha256"), hash);
        else File.Delete(Path.Combine(_factory.Folder, "client.sha256"));
    }

    [Fact]
    public async Task Nothing_is_offered_until_a_build_is_published()
    {
        foreach (var f in Directory.GetFiles(_factory.Folder)) File.Delete(f);
        var http = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/client/version")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/client/package")).StatusCode);
    }

    [Fact]
    public async Task A_published_build_is_described_and_downloadable_without_signing_in()
    {
        var bytes = Enumerable.Range(0, 5000).Select(i => (byte)(i % 251)).ToArray();
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Publish("1.4.2", bytes);   // no client.sha256: the server computes it

        var http = _factory.CreateClient();
        var info = (await http.GetFromJsonAsync<ClientVersionInfo>("/api/client/version", Json))!;
        Assert.Equal("1.4.2", info.Version);
        Assert.Equal(expected, info.Sha256);
        Assert.Equal(bytes.Length, info.SizeBytes);

        var package = await http.GetAsync("/api/client/package");
        package.EnsureSuccessStatusCode();
        Assert.Equal(bytes, await package.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task The_recorded_checksum_is_used_and_a_garbled_version_is_refused()
    {
        var recorded = new string('a', 64);
        Publish("2.0.0", new byte[] { 1, 2, 3 }, recorded);
        var http = _factory.CreateClient();
        Assert.Equal(recorded, (await http.GetFromJsonAsync<ClientVersionInfo>("/api/client/version", Json))!.Sha256);

        Publish("../../etc/passwd", new byte[] { 1, 2, 3 });
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/client/version")).StatusCode);
    }
}
