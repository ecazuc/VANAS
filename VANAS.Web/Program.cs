using Microsoft.AspNetCore.Components.Authorization;
using VANAS.Web.Components;
using VANAS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<StaffAuthState>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<StaffAuthState>());

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5049");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.MapGet("/rss.xml", async (HttpContext context, ApiClient api) =>
{
    var result = await api.GetArticlesAsync(1, null, null, null);
    var items = result.Value?.Items ?? new List<VANAS.Shared.Contracts.ArticleSummary>();
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

    var settings = new System.Xml.XmlWriterSettings { Async = true, Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
    using var stream = new MemoryStream();
    await using (var xml = System.Xml.XmlWriter.Create(stream, settings))
    {
        await xml.WriteStartDocumentAsync();
        xml.WriteStartElement("rss");
        xml.WriteAttributeString("version", "2.0");
        xml.WriteStartElement("channel");
        xml.WriteElementString("title", "VANAS - Actus");
        xml.WriteElementString("link", baseUrl + "/blog");
        xml.WriteElementString("description", "L'actualité du jeu vidéo et la vie du café VANAS.");
        xml.WriteElementString("language", "fr-FR");

        foreach (var a in items)
        {
            xml.WriteStartElement("item");
            xml.WriteElementString("title", a.Title);
            xml.WriteElementString("link", $"{baseUrl}/blog/{a.Slug}");
            xml.WriteElementString("guid", $"{baseUrl}/blog/{a.Slug}");
            xml.WriteElementString("description", a.Summary);
            if (a.PublishedAt is DateTime published)
                xml.WriteElementString("pubDate", DateTime.SpecifyKind(published, DateTimeKind.Utc).ToString("R"));
            xml.WriteEndElement();
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        await xml.WriteEndDocumentAsync();
    }
    return Results.Text(System.Text.Encoding.UTF8.GetString(stream.ToArray()), "application/rss+xml; charset=utf-8");
});
app.Run();
