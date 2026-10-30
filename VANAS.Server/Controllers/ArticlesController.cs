using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Controllers
{
    [ApiController]
    [Route("api/articles")]
    public class ArticlesController : ControllerBase
    {
        private const int MaxPageSize = 50;

        private readonly AppDbContext _db;

        public ArticlesController(AppDbContext db) => _db = db;

        private static ArticleSummary ToSummary(Article a) =>
            new(a.Id, a.Title, a.Slug, a.Summary, a.Category, a.Tags, a.CoverUrl, a.IsPublished, a.PublishedAt);

        private static ArticleDetail ToDetail(Article a) =>
            new(a.Id, a.Title, a.Slug, a.Summary, a.ContentMarkdown, a.Category, a.Tags, a.CoverUrl, a.IsPublished, a.PublishedAt, a.UpdatedAt);

        // ---------- Public ----------

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetPublished([FromQuery] int page = 1, [FromQuery] int pageSize = 9,
            [FromQuery] string? category = null, [FromQuery] string? tag = null, [FromQuery] string? q = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _db.Articles.Where(a => a.IsPublished);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(a => a.Category == category);
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var t = "," + tag.Trim().ToLower() + ",";
                query = query.Where(a => ("," + a.Tags + ",").Contains(t));
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLower();
                query = query.Where(a => a.Title.ToLower().Contains(s) || a.Summary.ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new ArticlePage(items.Select(ToSummary).ToList(), page, pageSize, total));
        }

        [AllowAnonymous]
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories() =>
            Ok(await _db.Articles.Where(a => a.IsPublished && a.Category != "").Select(a => a.Category).Distinct().OrderBy(c => c).ToListAsync());

        [AllowAnonymous]
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Slug == slug && a.IsPublished);
            return article == null ? NotFound("Article introuvable.") : Ok(ToDetail(article));
        }

        // ---------- Staff ----------

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage")]
        public async Task<IActionResult> GetAll() =>
            Ok((await _db.Articles.OrderByDescending(a => a.UpdatedAt).ToListAsync()).Select(ToSummary));

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("manage/{id:int}")]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var article = await _db.Articles.FindAsync(id);
            return article == null ? NotFound("Article introuvable.") : Ok(ToDetail(article));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveArticleRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var article = new Article { AuthorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) };
            await ApplyAsync(article, request, existingId: null);
            _db.Articles.Add(article);
            await _db.SaveChangesAsync();
            return Ok(ToDetail(article));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveArticleRequest request)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(error);

            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound("Article introuvable.");

            await ApplyAsync(article, request, existingId: id);
            await _db.SaveChangesAsync();
            return Ok(ToDetail(article));
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound("Article introuvable.");
            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            return Ok();
        }

        private async Task ApplyAsync(Article article, SaveArticleRequest r, int? existingId)
        {
            var wasPublished = article.IsPublished;
            var title = r.Title.Trim();

            // The slug is generated once (so published links never change) and kept unique.
            if (string.IsNullOrEmpty(article.Slug))
            {
                var baseSlug = Slugger.Create(title);
                var slug = baseSlug;
                for (var n = 2; await _db.Articles.AnyAsync(a => a.Slug == slug && a.Id != (existingId ?? 0)); n++)
                    slug = $"{baseSlug}-{n}";
                article.Slug = slug;
            }

            article.Title = title;
            article.Summary = r.Summary.Trim();
            article.ContentMarkdown = r.ContentMarkdown;
            article.Category = r.Category.Trim();
            article.Tags = string.Join(',', r.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant()).Distinct());
            article.CoverUrl = string.IsNullOrWhiteSpace(r.CoverUrl) ? null : r.CoverUrl.Trim();
            article.IsPublished = r.IsPublished;
            if (r.IsPublished && (!wasPublished || article.PublishedAt == null)) article.PublishedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;
        }

        private static string? Validate(SaveArticleRequest r)
        {
            if ((r.Title?.Trim().Length ?? 0) is < 3 or > 120) return "Le titre doit faire entre 3 et 120 caractères.";
            if ((r.Summary?.Length ?? 0) > 400) return "Le résumé est limité à 400 caractères.";
            if (string.IsNullOrWhiteSpace(r.ContentMarkdown)) return "Le contenu est vide.";
            if (r.ContentMarkdown.Length > 100_000) return "Le contenu est trop long.";
            if ((r.Category?.Length ?? 0) > 40) return "La catégorie est limitée à 40 caractères.";
            if ((r.Tags?.Length ?? 0) > 200) return "Les tags sont limités à 200 caractères.";

            if (!string.IsNullOrWhiteSpace(r.CoverUrl) &&
                !(Uri.TryCreate(r.CoverUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
                return "L'image de couverture doit être une adresse http(s).";
            return null;
        }
    }
}
