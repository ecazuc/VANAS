using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Web.Services
{
    /// <summary>Keeps the staff member's JWT in the browser tab (session storage) and exposes it as the Blazor user.</summary>
    public class StaffAuthState : AuthenticationStateProvider
    {
        private const string StorageKey = "vanas.staff";
        private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

        private readonly ProtectedSessionStorage _storage;
        private ClaimsPrincipal _user = Anonymous;
        private bool _loaded;

        public StaffAuthState(ProtectedSessionStorage storage) => _storage = storage;

        public string? Token { get; private set; }
        public string? Username { get; private set; }
        public UserRole Role { get; private set; }
        public bool IsAdmin => Role == UserRole.Admin;

        private record Stored(string Token, string Username, UserRole Role, DateTime ExpiresAt);

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (!_loaded)
            {
                try
                {
                    var result = await _storage.GetAsync<Stored>(StorageKey);
                    if (result.Success && result.Value is { } s && s.ExpiresAt > DateTime.UtcNow) Apply(s);
                    _loaded = true;
                }
                catch (InvalidOperationException)
                {
                    // JS interop not available yet (static rendering): treat as signed out for now.
                }
            }
            return new AuthenticationState(_user);
        }

        public async Task SignInAsync(LoginResponse login)
        {
            var stored = new Stored(login.Token, login.Username, login.Role, DateTime.UtcNow.AddHours(11));
            await _storage.SetAsync(StorageKey, stored);
            _loaded = true;
            Apply(stored);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));
        }

        public async Task SignOutAsync()
        {
            try { await _storage.DeleteAsync(StorageKey); } catch (InvalidOperationException) { }
            Token = null;
            Username = null;
            _user = Anonymous;
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));
        }

        private void Apply(Stored s)
        {
            Token = s.Token;
            Username = s.Username;
            Role = s.Role;
            _user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, s.Username),
                new Claim(ClaimTypes.Role, s.Role.ToString())
            }, "vanas-jwt"));
        }
    }
}
