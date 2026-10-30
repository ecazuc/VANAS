using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/catalog")]
    public class CatalogController : ControllerBase
    {
        /// <summary>External ids end up in launcher URIs and process arguments on customer PCs: keep them to a safe alphabet.</summary>
        private static readonly Regex SafeId = new(@"^[A-Za-z0-9_.\-]{1,100}$", RegexOptions.Compiled);

        private readonly AppDbContext _db;

        public CatalogController(AppDbContext db) => _db = db;

        private static CatalogGameDto ToDto(CatalogGame g) => new(g.Id, g.Title, g.Launcher, g.ExternalId, g.IsEnabled);

        /// <summary>Games customers may play (enabled only).</summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetEnabled() =>
            Ok((await _db.Games.Where(g => g.IsEnabled).OrderBy(g => g.Title).ToListAsync()).Select(ToDto));

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll() =>
            Ok((await _db.Games.OrderBy(g => g.Title).ToListAsync()).Select(ToDto));

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveCatalogGameRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var externalId = request.ExternalId.Trim();
            if (await _db.Games.AnyAsync(g => g.Launcher == request.Launcher && g.ExternalId == externalId))
                return BadRequest("Ce jeu est déjà dans le catalogue.");

            var game = new CatalogGame { Title = request.Title.Trim(), Launcher = request.Launcher, ExternalId = externalId, IsEnabled = request.IsEnabled };
            _db.Games.Add(game);
            await _db.SaveChangesAsync();
            return Ok(ToDto(game));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveCatalogGameRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var game = await _db.Games.FindAsync(id);
            if (game == null) return NotFound("Jeu introuvable.");

            var externalId = request.ExternalId.Trim();
            if (await _db.Games.AnyAsync(g => g.Id != id && g.Launcher == request.Launcher && g.ExternalId == externalId))
                return BadRequest("Ce jeu est déjà dans le catalogue.");

            game.Title = request.Title.Trim();
            game.Launcher = request.Launcher;
            game.ExternalId = externalId;
            game.IsEnabled = request.IsEnabled;
            await _db.SaveChangesAsync();
            return Ok(ToDto(game));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var game = await _db.Games.FindAsync(id);
            if (game == null) return NotFound("Jeu introuvable.");
            _db.Games.Remove(game);
            await _db.SaveChangesAsync();
            return Ok();
        }

        private static string? Validate(SaveCatalogGameRequest r)
        {
            var title = r.Title?.Trim() ?? string.Empty;
            if (title.Length is < 1 or > 60) return "Le titre doit faire entre 1 et 60 caractères.";
            if (!Enum.IsDefined(r.Launcher)) return "Launcher inconnu.";
            if (!SafeId.IsMatch(r.ExternalId?.Trim() ?? string.Empty))
                return "L'identifiant doit faire 1 à 100 caractères (lettres, chiffres, _ . -).";
            return null;
        }
    }
}
