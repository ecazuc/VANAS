using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/cafe")]
    public class CafeController : ControllerBase
    {
        private const int MaxPhotos = 8;

        private readonly AppDbContext _db;

        public CafeController(AppDbContext db) => _db = db;

        private static List<string> Photos(string raw) =>
            raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private static CafeInfoDto ToDto(CafeInfo c) => new(c.OpeningHours, c.Address, c.Phone, c.Email, c.AboutMarkdown, Photos(c.PhotoUrls));

        /// <summary>Shown to everyone on the website. Empty fields until an admin fills them in.</summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(ToDto(await _db.CafeInfo.FindAsync(1) ?? new CafeInfo()));

        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveCafeInfoRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var info = await _db.CafeInfo.FindAsync(1);
            if (info == null)
            {
                info = new CafeInfo { Id = 1 };
                _db.CafeInfo.Add(info);
            }

            info.OpeningHours = request.OpeningHours.Trim();
            info.Address = request.Address.Trim();
            info.Phone = request.Phone.Trim();
            info.Email = request.Email.Trim();
            info.AboutMarkdown = request.AboutMarkdown;
            info.PhotoUrls = string.Join('\n', Photos(request.PhotoUrls ?? string.Empty));
            info.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ToDto(info));
        }

        private static string? Validate(SaveCafeInfoRequest r)
        {
            if ((r.OpeningHours?.Length ?? 0) > 1000) return "Les horaires sont limités à 1000 caractères.";
            if ((r.Address?.Length ?? 0) > 300) return "L'adresse est limitée à 300 caractères.";
            if ((r.Phone?.Length ?? 0) > 40) return "Le téléphone est limité à 40 caractères.";
            if ((r.Email?.Length ?? 0) > 120 || (!string.IsNullOrWhiteSpace(r.Email) && !r.Email.Contains('@'))) return "L'adresse e-mail est invalide.";
            if ((r.AboutMarkdown?.Length ?? 0) > 20_000) return "Le texte de présentation est trop long.";

            var photos = Photos(r.PhotoUrls ?? string.Empty);
            if (photos.Count > MaxPhotos) return $"Maximum {MaxPhotos} photos.";
            foreach (var p in photos)
                if (!(Uri.TryCreate(p, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
                    return "Chaque photo doit être une adresse http(s).";
            return null;
        }
    }
}
