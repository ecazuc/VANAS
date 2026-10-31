using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Server.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokens;
        private readonly BalanceService _balance;

        private readonly AgeService _age;

        public AuthController(AppDbContext db, TokenService tokens, BalanceService balance, AgeService age)
        {
            _db = db;
            _tokens = tokens;
            _balance = balance;
            _age = age;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var name = request.Username.Trim().ToLower();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == name);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized("Invalid username or password.");

            return Ok(new LoginResponse(_tokens.Create(user), user.Username, user.Role, await _balance.GetMinutesAsync(user.Id), _age.ConsentPending(user)));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var username = request.Username.Trim();
            if (username.Length is < 3 or > 32)
                return BadRequest("Username must be between 3 and 32 characters.");
            if (request.Password.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            var birthProblem = _age.CheckBirthDate(request.BirthDate);
            if (birthProblem != null) return BadRequest(birthProblem);

            var lower = username.ToLower();
            if (await _db.Users.AnyAsync(u => u.Username.ToLower() == lower))
                return BadRequest("Username already taken.");

            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = UserRole.Client,
                BirthDate = request.BirthDate,
                Profile = new Profile { DisplayName = username }
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return Ok(new RegisterResult(_age.ConsentPending(user)));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(id);
            if (user == null) return Unauthorized();
            return Ok(new LoginResponse(string.Empty, user.Username, user.Role, await _balance.GetMinutesAsync(id), _age.ConsentPending(user)));
        }
    }
}
