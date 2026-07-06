using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemandesProprietaireController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DemandesProprietaireController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private string? CurrentRole() => User.FindFirst(ClaimTypes.Role)?.Value;

        private bool IsAdmin() => CurrentRole() is "AdminRegion" or "AdminPays" or "DirecteurProjet";

        // ─── POST /api/demandesproprietaire ───────────────────────────────────
        // Un Client soumet une demande de passage au statut Propriétaire
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create([FromBody] CreateDemandeRequest req)
        {
            var userId = CurrentUserId();

            bool dejaEnAttente = await _context.DemandesProprietaire
                .AnyAsync(d => d.UserId == userId && d.Statut == "En attente");

            if (dejaEnAttente)
                return BadRequest(new { success = false, message = "Vous avez déjà une demande en attente." });

            var demande = new DemandeProprietaire
            {
                UserId  = userId,
                Region  = req.Region,
                Message = req.Message,
            };

            _context.DemandesProprietaire.Add(demande);

            var demandeur = await _context.Users.FindAsync(userId);
            var adminsRegion = await _context.Users
                .Where(u => u.Role == "AdminRegion" && u.Region == req.Region)
                .ToListAsync();

            foreach (var admin in adminsRegion)
            {
                NotificationHelper.Creer(_context, admin.Id,
                    "NouvelleDemandeProprietaire", "Nouvelle demande Propriétaire",
                    $"{demandeur?.Prenom} {demandeur?.Nom} souhaite devenir Propriétaire dans la région {req.Region}.",
                    "demandes");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Votre demande a été envoyée à l'administration régionale.", data = new DemandeDto(demande, null) });
        }

        // ─── GET /api/demandesproprietaire/mine ───────────────────────────────
        // Le demandeur suit le statut de ses propres demandes
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var userId = CurrentUserId();

            var demandes = await _context.DemandesProprietaire
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DemandeDto(d, null))
                .ToListAsync();

            return Ok(new { success = true, data = demandes });
        }

        // ─── GET /api/demandesproprietaire ─────────────────────────────────────
        // Un AdminRegion ne voit que les demandes de sa région ; AdminPays/DirecteurProjet voient tout
        [HttpGet]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetAll([FromQuery] string? statut)
        {
            var query = _context.DemandesProprietaire.Include(d => d.User).AsQueryable();

            if (CurrentRole() == "AdminRegion")
            {
                var moi = await _context.Users.FindAsync(CurrentUserId());
                query = query.Where(d => d.Region == moi!.Region);
            }

            if (!string.IsNullOrWhiteSpace(statut))
                query = query.Where(d => d.Statut == statut);

            var demandes = await query
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DemandeDto(d, d.User))
                .ToListAsync();

            return Ok(new { success = true, data = demandes });
        }

        // ─── GET /api/demandesproprietaire/proprietaires ──────────────────────
        // Liste des comptes Propriétaire (de la région de l'AdminRegion, ou toutes pour AdminPays/DirecteurProjet)
        [HttpGet("proprietaires")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetProprietaires()
        {
            var query = _context.Users.Where(u => u.Role == "Proprietaire").AsQueryable();

            if (CurrentRole() == "AdminRegion")
            {
                var moi = await _context.Users.FindAsync(CurrentUserId());
                query = query.Where(u => u.Region == moi!.Region);
            }

            var proprietaires = await query.OrderBy(u => u.Nom).ToListAsync();

            var data = new List<ProprietaireDto>();
            foreach (var p in proprietaires)
            {
                var nbBiens = await _context.Biens.CountAsync(b => b.ProprietaireId == p.Id);
                var derniereValidation = await _context.DemandesProprietaire
                    .Where(d => d.UserId == p.Id && d.Statut == "Validée")
                    .OrderByDescending(d => d.TraiteLe)
                    .Select(d => d.TraiteLe)
                    .FirstOrDefaultAsync();

                data.Add(new ProprietaireDto(p, nbBiens, derniereValidation));
            }

            return Ok(new { success = true, data });
        }

        // ─── PUT /api/demandesproprietaire/{id}/valider ───────────────────────
        [HttpPut("{id:int}/valider")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Valider(int id)
        {
            var demande = await _context.DemandesProprietaire
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound(new { success = false, message = "Demande introuvable." });

            if (demande.Statut != "En attente")
                return BadRequest(new { success = false, message = "Cette demande a déjà été traitée." });

            if (CurrentRole() == "AdminRegion")
            {
                var moi = await _context.Users.FindAsync(CurrentUserId());
                if (demande.Region != moi!.Region)
                    return Forbid();
            }

            demande.Statut          = "Validée";
            demande.TraiteParAdminId = CurrentUserId();
            demande.TraiteLe         = DateTime.UtcNow;
            demande.User!.Role      = "Proprietaire";
            demande.User.Region     = demande.Region;

            NotificationHelper.Creer(_context, demande.UserId,
                "DemandeValidee", "Demande Propriétaire validée",
                "Votre demande pour devenir Propriétaire a été validée ! Vous pouvez maintenant publier des annonces.",
                "devenir-proprietaire");

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"{demande.User.Prenom} {demande.User.Nom} est maintenant Propriétaire." });
        }

        // ─── PUT /api/demandesproprietaire/{id}/rejeter ───────────────────────
        [HttpPut("{id:int}/rejeter")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Rejeter(int id)
        {
            var demande = await _context.DemandesProprietaire.FirstOrDefaultAsync(d => d.Id == id);

            if (demande == null)
                return NotFound(new { success = false, message = "Demande introuvable." });

            if (demande.Statut != "En attente")
                return BadRequest(new { success = false, message = "Cette demande a déjà été traitée." });

            if (CurrentRole() == "AdminRegion")
            {
                var moi = await _context.Users.FindAsync(CurrentUserId());
                if (demande.Region != moi!.Region)
                    return Forbid();
            }

            demande.Statut           = "Refusée";
            demande.TraiteParAdminId = CurrentUserId();
            demande.TraiteLe         = DateTime.UtcNow;

            NotificationHelper.Creer(_context, demande.UserId,
                "DemandeRefusee", "Demande Propriétaire refusée",
                "Votre demande pour devenir Propriétaire a été refusée. Vous pouvez en soumettre une nouvelle.",
                "devenir-proprietaire");

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Demande refusée." });
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────
    public class DemandeDto
    {
        public int       Id        { get; set; }
        public int       UserId    { get; set; }
        public string?   Prenom    { get; set; }
        public string?   Nom       { get; set; }
        public string?   Email     { get; set; }
        public string    Region    { get; set; }
        public string?   Message   { get; set; }
        public string    Statut    { get; set; }
        public DateTime  CreatedAt { get; set; }

        public DemandeDto(DemandeProprietaire d, User? user)
        {
            Id        = d.Id;
            UserId    = d.UserId;
            Prenom    = user?.Prenom;
            Nom       = user?.Nom;
            Email     = user?.Email;
            Region    = d.Region;
            Message   = d.Message;
            Statut    = d.Statut;
            CreatedAt = d.CreatedAt;
        }
    }

    public class ProprietaireDto
    {
        public int       Id             { get; set; }
        public string    Prenom         { get; set; }
        public string    Nom            { get; set; }
        public string    Email          { get; set; }
        public string?   Region         { get; set; }
        public int       NbBiensPublies { get; set; }
        public DateTime? DateValidation { get; set; }

        public ProprietaireDto(User u, int nbBiens, DateTime? dateValidation)
        {
            Id             = u.Id;
            Prenom         = u.Prenom;
            Nom            = u.Nom;
            Email          = u.Email;
            Region         = u.Region;
            NbBiensPublies = nbBiens;
            DateValidation = dateValidation;
        }
    }

    // ─── REQUEST MODEL ────────────────────────────────────────────────────────
    public class CreateDemandeRequest
    {
        public string  Region  { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}
