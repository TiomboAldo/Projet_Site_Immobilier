using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentairesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CommentairesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private bool IsAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role is "AdminRegion" or "AdminPays" or "DirecteurProjet";
        }

        // ─── GET /api/commentaires?bienId={id} ────────────────────────────────
        // Public : tout visiteur peut lire les avis d'un bien
        [HttpGet]
        public async Task<IActionResult> GetByBien([FromQuery] int bienId)
        {
            var commentaires = await _context.Commentaires
                .Where(c => c.BienId == bienId)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var userIdsVerifies = await _context.Reservations
                .Where(r => r.BienId == bienId && r.Statut == "Confirmée")
                .Select(r => r.UserId)
                .Distinct()
                .ToListAsync();

            var data = commentaires.Select(c => new CommentaireDto(c, userIdsVerifies.Contains(c.UserId))).ToList();
            var moyenne = data.Count > 0 ? Math.Round(data.Average(c => c.Note), 1) : 0;

            return Ok(new { success = true, data = new { moyenne, total = data.Count, commentaires = data } });
        }

        // ─── POST /api/commentaires ─────────────────────────────────────────────
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateCommentaireRequest req)
        {
            if (req.Note < 1 || req.Note > 5)
                return BadRequest(new { success = false, message = "La note doit être comprise entre 1 et 5." });

            var userId = CurrentUserId();

            var bien = await _context.Biens.FindAsync(req.BienId);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            bool dejaCommente = await _context.Commentaires
                .AnyAsync(c => c.UserId == userId && c.BienId == req.BienId);
            if (dejaCommente)
                return BadRequest(new { success = false, message = "Vous avez déjà commenté ce bien." });

            var commentaire = new Commentaire
            {
                BienId = req.BienId,
                UserId = userId,
                Note   = req.Note,
                Texte  = req.Texte ?? string.Empty,
            };

            _context.Commentaires.Add(commentaire);
            await _context.SaveChangesAsync();

            var estVerifie = await _context.Reservations
                .AnyAsync(r => r.UserId == userId && r.BienId == req.BienId && r.Statut == "Confirmée");

            return Ok(new { success = true, message = "Avis publié.", data = new CommentaireDto(commentaire, estVerifie) });
        }

        // ─── PUT /api/commentaires/{id} ─────────────────────────────────────────
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] CreateCommentaireRequest req)
        {
            if (req.Note < 1 || req.Note > 5)
                return BadRequest(new { success = false, message = "La note doit être comprise entre 1 et 5." });

            var commentaire = await _context.Commentaires.FindAsync(id);
            if (commentaire == null)
                return NotFound(new { success = false, message = "Commentaire introuvable." });

            if (commentaire.UserId != CurrentUserId())
                return Forbid();

            commentaire.Note  = req.Note;
            commentaire.Texte = req.Texte ?? string.Empty;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Avis mis à jour." });
        }

        // ─── DELETE /api/commentaires/{id} ──────────────────────────────────────
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var commentaire = await _context.Commentaires.FindAsync(id);
            if (commentaire == null)
                return NotFound(new { success = false, message = "Commentaire introuvable." });

            if (commentaire.UserId != CurrentUserId() && !IsAdmin())
                return Forbid();

            _context.Commentaires.Remove(commentaire);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Avis supprimé." });
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────
    public class CommentaireDto
    {
        public int      Id        { get; set; }
        public int      UserId    { get; set; }
        public string?  Prenom    { get; set; }
        public string?  Nom       { get; set; }
        public int      Note      { get; set; }
        public string   Texte     { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool     EstLocataireVerifie { get; set; }

        public CommentaireDto(Commentaire c, bool estLocataireVerifie)
        {
            Id        = c.Id;
            UserId    = c.UserId;
            Prenom    = c.User?.Prenom;
            Nom       = c.User?.Nom;
            Note      = c.Note;
            Texte     = c.Texte;
            CreatedAt = c.CreatedAt;
            EstLocataireVerifie = estLocataireVerifie;
        }
    }

    // ─── REQUEST MODEL ────────────────────────────────────────────────────────
    public class CreateCommentaireRequest
    {
        public int     BienId { get; set; }
        public int     Note   { get; set; }
        public string? Texte  { get; set; }
    }
}
