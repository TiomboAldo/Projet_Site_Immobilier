using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BiensController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BiensController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private bool IsAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role is "AdminRegion" or "AdminPays" or "DirecteurProjet";
        }

        // ─── GET /api/biens ───────────────────────────────────────────────────
        // Paramètres optionnels : ?type=villa&statut=vente&standing=Elite&q=Douala
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? type,
            [FromQuery] string? statut,
            [FromQuery] string? standing,
            [FromQuery] string? q)
        {
            var query = _context.Biens.Where(b => b.EstDisponible).AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(b => b.Type.ToLower() == type.ToLower());

            if (!string.IsNullOrWhiteSpace(statut))
                query = query.Where(b => b.Statut.ToLower() == statut.ToLower());

            if (!string.IsNullOrWhiteSpace(standing))
                query = query.Where(b => b.Standing.ToLower() == standing.ToLower());

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(b =>
                    b.Titre.Contains(q) ||
                    b.Localisation.Contains(q) ||
                    b.Description.Contains(q));

            var biens = await query
                .OrderByDescending(b => b.DateAjout)
                .Select(b => new BienDto(b))
                .ToListAsync();

            return Ok(new { success = true, data = biens });
        }

        // ─── GET /api/biens/{id} ──────────────────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            bien.Vues += 1;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = new BienDto(bien) });
        }

        // ─── GET /api/biens/mine  (annonces du propriétaire connecté) ─────────
        [HttpGet("mine")]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetMine()
        {
            var userId = CurrentUserId();

            var biens = await _context.Biens
                .Where(b => b.ProprietaireId == userId)
                .OrderByDescending(b => b.DateAjout)
                .Select(b => new BienDto(b))
                .ToListAsync();

            return Ok(new { success = true, data = biens });
        }

        // ─── PUT /api/biens/{id}  (modifier sa propre annonce) ────────────────
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBienRequest req)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId() && !IsAdmin())
                return Forbid();

            bien.Titre         = req.Titre;
            bien.Type          = req.Type;
            bien.Statut        = req.Statut;
            bien.Prix          = req.Prix;
            bien.Chambres      = req.Chambres;
            bien.SallesDeBain  = req.SallesDeBain;
            bien.Surface       = req.Surface;
            bien.Localisation  = req.Localisation;
            bien.Description   = req.Description;
            bien.ImageUrl       = req.ImageUrl;
            bien.GalerieUrls    = req.GalerieUrls;
            bien.Equipements    = req.Equipements;
            bien.Standing       = req.Standing;
            bien.EstDisponible  = req.EstDisponible;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Bien mis à jour avec succès.", data = new BienDto(bien) });
        }

        // ─── DELETE /api/biens/{id}  (retirer sa propre annonce) ──────────────
        // Pas de suppression physique : Reservations/Favoris sont en CASCADE en base,
        // un vrai DELETE effacerait silencieusement leur historique. On désactive seulement.
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Deactivate(int id)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId() && !IsAdmin())
                return Forbid();

            bien.EstDisponible = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Annonce retirée." });
        }

        // ─── POST /api/biens  (Propriétaire / User indep / Admin uniquement) ──
        [HttpPost]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Create([FromBody] CreateBienRequest req)
        {
            var proprietaireId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var bien = new Bien
            {
                Titre        = req.Titre,
                Type         = req.Type,
                Statut       = req.Statut,
                Prix         = req.Prix,
                Chambres     = req.Chambres,
                SallesDeBain = req.SallesDeBain,
                Surface      = req.Surface,
                Localisation = req.Localisation,
                Description  = req.Description,
                ImageUrl     = req.ImageUrl,
                GalerieUrls  = req.GalerieUrls,
                Equipements  = req.Equipements,
                Standing     = req.Standing,
                ProprietaireId = proprietaireId,
            };

            _context.Biens.Add(bien);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Bien créé avec succès.", data = new BienDto(bien) });
        }
    }

    // ─── DTO ─────────────────────────────────────────────────────────────────
    // On expose un objet propre, avec les listes déjà parsées
    public class BienDto
    {
        public int      Id           { get; set; }
        public string   Titre        { get; set; }
        public string   Type         { get; set; }
        public string   Statut       { get; set; }
        public string   Prix         { get; set; }
        public int      Chambres     { get; set; }
        public int      SallesDeBain { get; set; }
        public int      Surface      { get; set; }
        public string   Localisation { get; set; }
        public string   Description  { get; set; }
        public string   ImageUrl     { get; set; }
        public string   Standing     { get; set; }
        public bool     EstDisponible{ get; set; }
        public DateTime DateAjout    { get; set; }
        public int      Vues         { get; set; }
        public int?     ProprietaireId { get; set; }

        // Listes parsées depuis les champs séparés par "|"
        public List<string> Galerie     { get; set; }
        public List<string> Equipements { get; set; }

        public BienDto(Bien b)
        {
            Id           = b.Id;
            Titre        = b.Titre;
            Type         = b.Type;
            Statut       = b.Statut;
            Prix         = b.Prix;
            Chambres     = b.Chambres;
            SallesDeBain = b.SallesDeBain;
            Surface      = b.Surface;
            Localisation = b.Localisation;
            Description  = b.Description;
            ImageUrl     = b.ImageUrl;
            Standing     = b.Standing;
            EstDisponible= b.EstDisponible;
            DateAjout    = b.DateAjout;
            Vues         = b.Vues;
            ProprietaireId = b.ProprietaireId;
            Galerie      = string.IsNullOrEmpty(b.GalerieUrls)
                               ? new List<string>()
                               : b.GalerieUrls.Split('|').ToList();
            Equipements  = string.IsNullOrEmpty(b.Equipements)
                               ? new List<string>()
                               : b.Equipements.Split('|').ToList();
        }
    }

    // ─── REQUEST MODELS ───────────────────────────────────────────────────────
    public class CreateBienRequest
    {
        public string Titre        { get; set; } = string.Empty;
        public string Type         { get; set; } = string.Empty;
        public string Statut       { get; set; } = string.Empty;
        public string Prix         { get; set; } = string.Empty;
        public int    Chambres     { get; set; }
        public int    SallesDeBain { get; set; }
        public int    Surface      { get; set; }
        public string Localisation { get; set; } = string.Empty;
        public string Description  { get; set; } = string.Empty;
        public string ImageUrl     { get; set; } = string.Empty;
        public string GalerieUrls  { get; set; } = string.Empty;
        public string Equipements  { get; set; } = string.Empty;
        public string Standing     { get; set; } = string.Empty;
    }

    public class UpdateBienRequest
    {
        public string Titre        { get; set; } = string.Empty;
        public string Type         { get; set; } = string.Empty;
        public string Statut       { get; set; } = string.Empty;
        public string Prix         { get; set; } = string.Empty;
        public int    Chambres     { get; set; }
        public int    SallesDeBain { get; set; }
        public int    Surface      { get; set; }
        public string Localisation { get; set; } = string.Empty;
        public string Description  { get; set; } = string.Empty;
        public string ImageUrl     { get; set; } = string.Empty;
        public string GalerieUrls  { get; set; } = string.Empty;
        public string Equipements  { get; set; } = string.Empty;
        public string Standing     { get; set; } = string.Empty;
        public bool   EstDisponible { get; set; } = true;
    }
}