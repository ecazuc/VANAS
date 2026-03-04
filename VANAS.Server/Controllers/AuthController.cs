using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Shared.Models;
using VANAS.Server.Data;
using Microsoft.AspNetCore.Identity.Data;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // 2. Use BCrypt to verify the incoming password against the stored hash
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid username or password.");
            }

            return Ok(new
            {
                Username = user.Username,
                MinutesLeft = user.BalanceMinutes,
                IsAdmin = user.IsAdmin
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Username already taken.");
            }

            // 1. Generate a secure hash
            string salt = BCrypt.Net.BCrypt.GenerateSalt();
            string hashedName = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);

            var newUser = new User
            {
                Username = request.Username,
                PasswordHash = hashedName, // Store the hash, not the secret!
                BalanceMinutes = request.BalanceMinutes,
                IsAdmin = false, // Default to false for security
                CreatedAt = DateTime.UtcNow // Always use UtcNow for server timestamps
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

        // 2. Add time to a user
        [HttpPost("add-time")]
        public async Task<IActionResult> AddTime([FromBody] AddTimeRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null) return NotFound("User not found");

            user.BalanceMinutes += request.MinutesToAdd;
            await _context.SaveChangesAsync();

            return Ok(new { NewBalance = user.BalanceMinutes });
        }

        [HttpPut("update-balance")]
        public async Task<IActionResult> UpdateBalance([FromBody] UpdateBalanceRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null) return NotFound("User not found");

            // Subtract the minute used
            user.BalanceMinutes -= request.MinutesUsed;

            // Prevent negative balance (unless it's a 'kick' signal)
            if (user.BalanceMinutes < 0 && user.BalanceMinutes != -1)
                user.BalanceMinutes = 0;

            await _context.SaveChangesAsync();
            return Ok(new { RemainingMinutes = user.BalanceMinutes });
        }

        [HttpPut("set-admin")]
        public async Task<IActionResult> SetAdmin([FromBody] SetAdminRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null) return NotFound("User not found");
            user.IsAdmin = request.IsAdmin;
            await _context.SaveChangesAsync();
            return Ok(new { Username = user.Username, IsAdmin = user.IsAdmin });
        }

        [HttpDelete("users/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("User not found");
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("kick/{username}")]
        public async Task<IActionResult> KickUser(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("User not found");
            // Set balance to -1 to signal the client to kick them out
            user.BalanceMinutes = -1;
            await _context.SaveChangesAsync();
            return Ok();
        }

    }

    public class SetAdminRequest
    {
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public double BalanceMinutes { get; set; }
    }

    public class AddTimeRequest
    {
        public string Username { get; set; } = string.Empty;
        public double MinutesToAdd { get; set; }
    }

    public class UpdateBalanceRequest
    {
        public string Username { get; set; } = string.Empty;
        public double MinutesUsed { get; set; }
    }
}
