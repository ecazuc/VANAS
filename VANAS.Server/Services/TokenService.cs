using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VANAS.Shared.Models;

namespace VANAS.Server.Services
{
    public class TokenService
    {
        private readonly SymmetricSecurityKey _key;
        private readonly TimeSpan _lifetime;

        public TokenService(IConfiguration config)
        {
            _key = CreateKey(config);
            _lifetime = TimeSpan.FromHours(config.GetValue("Jwt:LifetimeHours", 12));
        }

        public static SymmetricSecurityKey CreateKey(IConfiguration config)
        {
            var secret = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }

        public string Create(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.Add(_lifetime),
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
