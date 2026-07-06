using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AvisController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AvisController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private string? CurrentRole() => User.FindFirst(ClaimTypes.Role)?.Value;

        // ─── GET /api/avis/bien/{bienId}  — public ───────────────────────────
        [HttpGet("bien/{bienId}")]
        public async Task<IActionResult> GetForBien(int bienId)
        {
            var avis = await _context.Commentaires
                .Where(c => c.BienId == bienId)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new AvisDto
                {
                    Id           = c.Id,
                    BienId       = c.BienId,
                    UserId       = c.UserId,
                    PrenomAuteur = c.User != null ? c.User.Prenom : "Anonyme",
                    NomAuteur    = c.User != null ? c.User.Nom    : "",
                    Note         = c.Note,
                    Texte        = c.Texte,
                    CreatedAt    = c.CreatedAt,
                })
                .ToListAsync();

            var moyenne = avis.Any() ? Math.Round(avis.Average(a => a.Note), 1) : 0.0;

            return Ok(new { success = true, data = avis, moyenne, total = avis.Count });
        }

        // ─── GET /api/avis/can-review/{bienId}  — vérifie si l'utilisateur peut laisser un avis
        [HttpGet("can-review/{bienId}")]
        [Authorize]
        public async Task<IActionResult> CanReview(int bienId)
        {
            var userId = CurrentUserId();

            var hasConfirmed = await _context.Reservations
                .AnyAsync(r => r.UserId == userId && r.BienId == bienId && r.Statut == "Confirmée");

            var alreadyReviewed = await _context.Commentaires
                .AnyAsync(c => c.UserId == userId && c.BienId == bienId);

            return Ok(new
            {
                success    = true,
                canReview  = hasConfirmed && !alreadyReviewed,
                hasVisited = hasConfirmed,
                hasReviewed = alreadyReviewed,
            });
        }

        // ─── POST /api/avis  — client avec visite confirmée ──────────────────
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateAvisRequest req)
        {
            var userId = CurrentUserId();

            var hasConfirmed = await _context.Reservations
                .AnyAsync(r => r.UserId == userId && r.BienId == req.BienId && r.Statut == "Confirmée");

            if (!hasConfirmed)
                return BadRequest(new { success = false, message = "Une visite confirmée est requise pour laisser un avis." });

            var alreadyReviewed = await _context.Commentaires
                .AnyAsync(c => c.UserId == userId && c.BienId == req.BienId);

            if (alreadyReviewed)
                return BadRequest(new { success = false, message = "Vous avez déjà laissé un avis pour ce bien." });

            if (req.Note < 1 || req.Note > 5)
                return BadRequest(new { success = false, message = "La note doit être entre 1 et 5." });

            var avis = new Commentaire
            {
                BienId = req.BienId,
                UserId = userId,
                Note   = req.Note,
                Texte  = req.Texte?.Trim() ?? string.Empty,
            };

            _context.Commentaires.Add(avis);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId);
            return Ok(new
            {
                success = true,
                message = "Merci pour votre avis !",
                data = new AvisDto
                {
                    Id           = avis.Id,
                    BienId       = avis.BienId,
                    UserId       = avis.UserId,
                    PrenomAuteur = user?.Prenom ?? "Anonyme",
                    NomAuteur    = user?.Nom    ?? "",
                    Note         = avis.Note,
                    Texte        = avis.Texte,
                    CreatedAt    = avis.CreatedAt,
                }
            });
        }

        // ─── DELETE /api/avis/{id}  — auteur ou admin ────────────────────────
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var avis = await _context.Commentaires.FindAsync(id);
            if (avis == null) return NotFound(new { success = false, message = "Avis introuvable." });

            var userId = CurrentUserId();
            var role   = CurrentRole();
            if (avis.UserId != userId && role is not ("AdminRegion" or "AdminPays" or "DirecteurProjet"))
                return Forbid();

            _context.Commentaires.Remove(avis);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Avis supprimé." });
        }
    }

    public class AvisDto
    {
        public int      Id           { get; set; }
        public int      BienId       { get; set; }
        public int      UserId       { get; set; }
        public string   PrenomAuteur { get; set; } = string.Empty;
        public string   NomAuteur    { get; set; } = string.Empty;
        public int      Note         { get; set; }
        public string   Texte        { get; set; } = string.Empty;
        public DateTime CreatedAt    { get; set; }
    }

    public class CreateAvisRequest
    {
        public int    BienId { get; set; }
        public int    Note   { get; set; }
        public string? Texte { get; set; }
    }
}
