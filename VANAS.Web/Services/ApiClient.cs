using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Web.Services
{
    public record ApiResult(string? Error)
    {
        public bool Ok => Error == null;
    }

    public record ApiResult<T>(T? Value, string? Error) : ApiResult(Error);

    /// <summary>Calls the VANAS API on behalf of the signed-in staff member.</summary>
    public class ApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;
        private readonly StaffAuthState _auth;

        public ApiClient(HttpClient http, StaffAuthState auth)
        {
            _http = http;
            _auth = auth;
        }

        public Task<ApiResult<LoginResponse>> LoginAsync(string username, string password) =>
            SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new LoginRequest(username, password), authenticated: false);

        public Task<ApiResult<List<StationStatus>>> GetStationsAsync() =>
            SendAsync<List<StationStatus>>(HttpMethod.Get, "api/stations");

        public Task<ApiResult<List<UserSummary>>> GetUsersAsync() =>
            SendAsync<List<UserSummary>>(HttpMethod.Get, "api/users");

        public Task<ApiResult> AddTimeAsync(int userId, double minutes, string? note = null) =>
            SendAsync(HttpMethod.Post, $"api/users/{userId}/add-time", new AddTimeRequest(minutes, note));

        public Task<ApiResult> KickAsync(int userId) =>
            SendAsync(HttpMethod.Post, $"api/users/{userId}/kick");

        public Task<ApiResult> SetRoleAsync(int userId, UserRole role) =>
            SendAsync(HttpMethod.Put, $"api/users/{userId}/role", new SetRoleRequest(role));

        public Task<ApiResult> DeleteUserAsync(int userId) =>
            SendAsync(HttpMethod.Delete, $"api/users/{userId}");

        public Task<ApiResult<List<CatalogGameDto>>> GetCatalogAsync() =>
            SendAsync<List<CatalogGameDto>>(HttpMethod.Get, "api/catalog/all");

        public Task<ApiResult> SaveGameAsync(int? id, SaveCatalogGameRequest request) =>
            id is int existing
                ? SendAsync(HttpMethod.Put, $"api/catalog/{existing}", request)
                : SendAsync(HttpMethod.Post, "api/catalog", request);

        public Task<ApiResult> DeleteGameAsync(int id) =>
            SendAsync(HttpMethod.Delete, $"api/catalog/{id}");

        // ----- Blog (public reads need no token) -----

        public Task<ApiResult<ArticlePage>> GetArticlesAsync(int page, string? category, string? tag, string? search)
        {
            var query = new List<string> { $"page={page}" };
            if (!string.IsNullOrWhiteSpace(category)) query.Add("category=" + Uri.EscapeDataString(category));
            if (!string.IsNullOrWhiteSpace(tag)) query.Add("tag=" + Uri.EscapeDataString(tag));
            if (!string.IsNullOrWhiteSpace(search)) query.Add("q=" + Uri.EscapeDataString(search));
            return SendAsync<ArticlePage>(HttpMethod.Get, "api/articles?" + string.Join('&', query), authenticated: false);
        }

        public Task<ApiResult<List<string>>> GetArticleCategoriesAsync() =>
            SendAsync<List<string>>(HttpMethod.Get, "api/articles/categories", authenticated: false);

        public Task<ApiResult<ArticleDetail>> GetArticleAsync(string slug) =>
            SendAsync<ArticleDetail>(HttpMethod.Get, $"api/articles/{Uri.EscapeDataString(slug)}", authenticated: false);

        public Task<ApiResult<List<ArticleSummary>>> GetManagedArticlesAsync() =>
            SendAsync<List<ArticleSummary>>(HttpMethod.Get, "api/articles/manage");

        public Task<ApiResult<ArticleDetail>> GetArticleForEditAsync(int id) =>
            SendAsync<ArticleDetail>(HttpMethod.Get, $"api/articles/manage/{id}");

        public Task<ApiResult<ArticleDetail>> SaveArticleAsync(int? id, SaveArticleRequest request) =>
            id is int existing
                ? SendAsync<ArticleDetail>(HttpMethod.Put, $"api/articles/{existing}", request)
                : SendAsync<ArticleDetail>(HttpMethod.Post, "api/articles", request);

        public Task<ApiResult> DeleteArticleAsync(int id) =>
            SendAsync(HttpMethod.Delete, $"api/articles/{id}");

        public Task<ApiResult> RegisterAsync(string username, string password) =>
            SendAsync(HttpMethod.Post, "api/auth/register", new RegisterRequest(username, password), authenticated: false);

        private async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null, bool authenticated = true)
        {
            var (response, error) = await RawAsync(method, url, body, authenticated);
            return new ApiResult(error);
        }

        private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null, bool authenticated = true)
        {
            var (response, error) = await RawAsync(method, url, body, authenticated);
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

        private async Task<(HttpResponseMessage? Response, string? Error)> RawAsync(HttpMethod method, string url, object? body, bool authenticated)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null) request.Content = JsonContent.Create(body, options: Json);
            if (authenticated && _auth.Token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);

            try
            {
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode) return (response, null);

                if (authenticated && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _auth.SignOutAsync();
                    return (null, "Session expirée, reconnectez-vous.");
                }

                var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
                if (!string.IsNullOrEmpty(text) && !text.StartsWith('{') && !text.StartsWith('<')) return (null, text);

                return (null, response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "Action réservée à un rôle supérieur.",
                    HttpStatusCode.NotFound => "Élément introuvable.",
                    _ => $"Erreur serveur ({(int)response.StatusCode})."
                });
            }
            catch (HttpRequestException)
            {
                return (null, "Serveur VANAS injoignable.");
            }
            catch (TaskCanceledException)
            {
                return (null, "Le serveur ne répond pas.");
            }
        }
    }
}
