using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RecommandationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RecommandationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ─── POST /api/recommandations ──────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRecommandationRequest req)
        {
            var expediteurId = CurrentUserId();

            var bien = await _context.Biens.FindAsync(req.BienId);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            var destinataire = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == (req.DestinataireEmail ?? string.Empty).Trim().ToLower());
            if (destinataire == null)
                return NotFound(new { success = false, message = "Aucun utilisateur trouvé avec cet email." });

            if (destinataire.Id == expediteurId)
                return BadRequest(new { success = false, message = "Vous ne pouvez pas vous recommander un bien à vous-même." });

            bool dejaRecommande = await _context.Recommandations.AnyAsync(r =>
                r.BienId == req.BienId && r.ExpediteurId == expediteurId && r.DestinataireId == destinataire.Id);
            if (dejaRecommande)
                return BadRequest(new { success = false, message = "Vous avez déjà recommandé ce bien à cette personne." });

            var recommandation = new Recommandation
            {
                BienId         = req.BienId,
                ExpediteurId   = expediteurId,
                DestinataireId = destinataire.Id,
                Message        = req.Message,
            };

            _context.Recommandations.Add(recommandation);

            var expediteur = await _context.Users.FindAsync(expediteurId);
            NotificationHelper.Creer(_context, destinataire.Id,
                "NouvelleRecommandation", "Nouvelle recommandation",
                $"{expediteur?.Prenom} {expediteur?.Nom} vous a recommandé « {bien.Titre} ».",
                "recommandations");

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Bien recommandé à {destinataire.Prenom} {destinataire.Nom}." });
        }

        // ─── GET /api/recommandations/recues ────────────────────────────────────
        [HttpGet("recues")]
        public async Task<IActionResult> GetRecues()
        {
            var userId = CurrentUserId();

            var recommandations = await _context.Recommandations
                .Where(r => r.DestinataireId == userId)
                .Include(r => r.Bien)
                .Include(r => r.Expediteur)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var data = recommandations.Select(r => new RecommandationDto(r)).ToList();

            return Ok(new { success = true, data });
        }

        // ─── PUT /api/recommandations/{id}/lue ──────────────────────────────────
        [HttpPut("{id:int}/lue")]
        public async Task<IActionResult> MarquerLue(int id)
        {
            var recommandation = await _context.Recommandations.FindAsync(id);
            if (recommandation == null)
                return NotFound(new { success = false, message = "Recommandation introuvable." });

            if (recommandation.DestinataireId != CurrentUserId())
                return Forbid();

            recommandation.EstLue = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // ─── DELETE /api/recommandations/{id} ───────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var recommandation = await _context.Recommandations.FindAsync(id);
            if (recommandation == null)
                return NotFound(new { success = false, message = "Recommandation introuvable." });

            if (recommandation.DestinataireId != CurrentUserId())
                return Forbid();

            _context.Recommandations.Remove(recommandation);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Recommandation supprimée." });
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────
    public class RecommandationDto
    {
        public int      Id           { get; set; }
        public int      BienId       { get; set; }
        public string?  BienTitre    { get; set; }
        public string?  BienImageUrl { get; set; }
        public string?  BienPrix     { get; set; }
        public string?  BienLocalisation { get; set; }
        public string?  ExpediteurPrenom { get; set; }
        public string?  ExpediteurNom    { get; set; }
        public string?  Message      { get; set; }
        public bool     EstLue       { get; set; }
        public DateTime CreatedAt    { get; set; }

        public RecommandationDto(Recommandation r)
        {
            Id               = r.Id;
            BienId           = r.BienId;
            BienTitre        = r.Bien?.Titre;
            BienImageUrl     = r.Bien?.ImageUrl;
            BienPrix         = r.Bien?.Prix;
            BienLocalisation = r.Bien?.Localisation;
            ExpediteurPrenom = r.Expediteur?.Prenom;
            ExpediteurNom    = r.Expediteur?.Nom;
            Message          = r.Message;
            EstLue           = r.EstLue;
            CreatedAt        = r.CreatedAt;
        }
    }

    // ─── REQUEST MODEL ────────────────────────────────────────────────────────
    public class CreateRecommandationRequest
    {
        public int     BienId           { get; set; }
        public string? DestinataireEmail { get; set; }
        public string? Message          { get; set; }
    }
}
