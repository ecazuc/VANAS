using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Mobile.Services;

public record ApiResult(string? Error)
{
    public bool Ok => Error == null;
}

public record ApiResult<T>(T? Value, string? Error) : ApiResult(Error);

/// <summary>Talks to the VANAS API and keeps the signed-in player's token in the phone's secure storage.</summary>
public class ApiService
{
    private const string TokenKey = "vanas.token";
    private const string ServerKey = "vanas.server";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private string? _token;

    /// <summary>Android emulators reach the PC through 10.0.2.2; a real phone needs the café server's address on the local network.</summary>
    public static string DefaultServer =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5049" : "http://localhost:5049";

    public string ServerUrl
    {
        get => Preferences.Get(ServerKey, DefaultServer);
        set => Preferences.Set(ServerKey, NormalizeServer(value));
    }

    public bool IsSignedIn => _token != null;

    /// <summary>Used by the live connection to the server (SignalR).</summary>
    public string? Token => _token;

    public UserRole Role { get; private set; } = UserRole.Client;

    public bool IsStaff => Role is UserRole.Staff or UserRole.Admin;

    public static string NormalizeServer(string value)
    {
        var v = (value ?? string.Empty).Trim().TrimEnd('/');
        if (v.Length == 0) return DefaultServer;
        return v.Contains("://") ? v : "http://" + v;
    }

    public static bool IsValidServer(string value) =>
        Uri.TryCreate(NormalizeServer(value), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <summary>Loads a previously saved token and checks the server still accepts it.</summary>
    public async Task<bool> TryRestoreAsync()
    {
        try { _token = await SecureStorage.Default.GetAsync(TokenKey); }
        catch (Exception) { _token = null; }
        if (_token == null) return false;

        // A 401 clears the token inside RawAsync; an unreachable server keeps it for the next attempt.
        var me = await SendAsync<LoginResponse>(HttpMethod.Get, "api/auth/me");
        if (me.Ok && me.Value != null) Role = me.Value.Role;
        return me.Ok;
    }

    public async Task<ApiResult> LoginAsync(string username, string password)
    {
        var result = await SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new LoginRequest(username, password), authenticated: false);
        if (!result.Ok || result.Value == null) return new ApiResult(Translate(result.Error) ?? "Connexion impossible.");

        _token = result.Value.Token;
        Role = result.Value.Role;
        try { await SecureStorage.Default.SetAsync(TokenKey, _token); }
        catch (Exception) { /* secure storage can be unavailable (e.g. no lock screen): stay signed in for this run only */ }
        return new ApiResult((string?)null);
    }

    public async Task<ApiResult> RegisterAsync(string username, string password)
    {
        var result = await SendAsync(HttpMethod.Post, "api/auth/register", new RegisterRequest(username, password), authenticated: false);
        return new ApiResult(Translate(result.Error));
    }

    public Task<ApiResult<ProfileInfo>> GetProfileAsync() =>
        SendAsync<ProfileInfo>(HttpMethod.Get, "api/profile/me");

    public Task<ApiResult<LiveStatus>> GetLiveAsync() =>
        SendAsync<LiveStatus>(HttpMethod.Get, "api/profile/me/live");

    public Task<ApiResult<List<SessionHistoryItem>>> GetSessionsAsync() =>
        SendAsync<List<SessionHistoryItem>>(HttpMethod.Get, "api/profile/me/sessions?take=10");

    public Task<ApiResult<List<EventSummary>>> GetEventsAsync() =>
        SendAsync<List<EventSummary>>(HttpMethod.Get, "api/events", authenticated: false);

    public Task<ApiResult<EventDetail>> GetEventAsync(int id) =>
        SendAsync<EventDetail>(HttpMethod.Get, $"api/events/{id}", authenticated: false);

    public Task<ApiResult<List<TicketInfo>>> GetMyTicketsAsync() =>
        SendAsync<List<TicketInfo>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<TicketInfo>> RegisterForEventAsync(int id) =>
        SendAsync<TicketInfo>(HttpMethod.Post, $"api/events/{id}/register");

    public Task<ApiResult> UnregisterFromEventAsync(int id) =>
        SendAsync(HttpMethod.Delete, $"api/events/{id}/register");

    public Task<ApiResult<CheckInResult>> CheckInAsync(string ticketCode) =>
        SendAsync<CheckInResult>(HttpMethod.Post, "api/events/check-in", new CheckInRequest(ticketCode));

    public void SignOut()
    {
        _token = null;
        Role = UserRole.Client;
        SecureStorage.Default.Remove(TokenKey);
    }

    private static string? Translate(string? error) => error switch
    {
        "Invalid username or password." => "Identifiant ou mot de passe incorrect.",
        "Username already taken." => "Cet identifiant est déjà pris.",
        "Username must be between 3 and 32 characters." => "L'identifiant doit faire entre 3 et 32 caractères.",
        "Password must be at least 8 characters." => "Le mot de passe doit faire au moins 8 caractères.",
        _ => error
    };

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null, bool authenticated = true)
    {
        var (_, error) = await RawAsync(method, path, body, authenticated);
        return new ApiResult(error);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, bool authenticated = true)
    {
        var (response, error) = await RawAsync(method, path, body, authenticated);
        if (error != null || response == null) return new ApiResult<T>(default, error ?? "Réponse vide.");
        try
        {
            return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(Json), null);
        }
        catch (JsonException)
        {
            return new ApiResult<T>(default, "Réponse du serveur illisible.");
        }
    }

    private async Task<(HttpResponseMessage? Response, string? Error)> RawAsync(HttpMethod method, string path, object? body, bool authenticated)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(ServerUrl + "/"), path));
        if (body != null) request.Content = JsonContent.Create(body, options: Json);
        if (authenticated && _token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        try
        {
            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode) return (response, null);

            if (authenticated && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                SignOut();
                return (null, "Session expirée, reconnectez-vous.");
            }

            var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            if (!string.IsNullOrEmpty(text) && !text.StartsWith('{') && !text.StartsWith('<')) return (null, text);
            return (null, $"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return (null, "Serveur VANAS injoignable. Vérifiez l'adresse du serveur et le Wi-Fi.");
        }
        catch (TaskCanceledException)
        {
            return (null, "Le serveur ne répond pas.");
        }
    }
}
