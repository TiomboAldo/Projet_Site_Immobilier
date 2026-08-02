using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend.Services;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemandesProprietaireController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService        _email;
        private readonly IWebHostEnvironment  _env;

        public DemandesProprietaireController(ApplicationDbContext context, IEmailService email, IWebHostEnvironment env)
        {
            _context = context;
            _email   = email;
            _env     = env;
        }

        private int     CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private string? CurrentRole()   => User.FindFirst(ClaimTypes.Role)?.Value;
        private bool    IsAdmin()       => CurrentRole() is "AdminRegion" or "AdminPays" or "DirecteurProjet";

        // ─── GET /api/demandesproprietaire/regions-with-admin ────────────────
        [HttpGet("regions-with-admin")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRegionsWithAdmin()
        {
            var regions = await _context.Users
                .Where(u => u.Role == "AdminRegion" && u.Region != null)
                .Select(u => u.Region!)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
            return Ok(new { success = true, data = regions });
        }

        // ─── POST /api/demandesproprietaire ───────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Client")]
        [RequestSizeLimit(15 * 1024 * 1024)] // 15 Mo max (document + selfie)
        public async Task<IActionResult> Create([FromForm] CreateDemandeRequest req)
        {
            var userId = CurrentUserId();

            bool dejaEnAttente = await _context.DemandesProprietaire
                .AnyAsync(d => d.UserId == userId && d.Statut == "En attente");

            if (dejaEnAttente)
                return BadRequest(new { success = false, message = "Vous avez déjà une demande en attente." });

            // Validation pièce d'identité
            if (req.Document == null || req.Document.Length == 0)
                return BadRequest(new { success = false, message = "Veuillez joindre une pièce d'identité." });
            if (req.Document.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "La pièce d'identité ne doit pas dépasser 5 Mo." });
            var allowedDoc = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
            if (!allowedDoc.Contains(req.Document.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Pièce d'identité : format accepté JPG, PNG ou PDF." });
            if (req.DocumentType is not ("CNI" or "Passeport"))
                return BadRequest(new { success = false, message = "Type de document invalide." });

            // Vérifier qu'un admin régional existe pour cette région
            bool adminExiste = await _context.Users
                .AnyAsync(u => u.Role == "AdminRegion" && u.Region == req.Region);
            if (!adminExiste)
                return BadRequest(new { success = false, message = $"La région '{req.Region}' ne dispose pas encore d'administrateur régional. Veuillez choisir une autre région ou contacter le support." });

            // Validation selfie
            if (req.Selfie == null || req.Selfie.Length == 0)
                return BadRequest(new { success = false, message = "Veuillez joindre votre selfie de vérification." });
            if (req.Selfie.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Le selfie ne doit pas dépasser 5 Mo." });
            var allowedSelfie = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedSelfie.Contains(req.Selfie.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Selfie : format accepté JPG ou PNG." });

            // Sauvegarde des fichiers
            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Demandes");
            Directory.CreateDirectory(uploadDir);

            var docExt      = Path.GetExtension(req.Document.FileName);
            var docFileName = $"{Guid.NewGuid()}{docExt}";
            using (var s = System.IO.File.Create(Path.Combine(uploadDir, docFileName)))
                await req.Document.CopyToAsync(s);

            var selfieFileName = $"{Guid.NewGuid()}.jpg";
            using (var s = System.IO.File.Create(Path.Combine(uploadDir, selfieFileName)))
                await req.Selfie.CopyToAsync(s);

            var demande = new DemandeProprietaire
            {
                UserId             = userId,
                Region             = req.Region,
                Message            = req.Message,
                DocumentType       = req.DocumentType,
                DocumentPath       = docFileName,
                SelfieDocumentPath = selfieFileName,
            };

            _context.DemandesProprietaire.Add(demande);

            var demandeur    = await _context.Users.FindAsync(userId);
            var adminsRegion = await _context.Users
                .Where(u => u.Role == "AdminRegion" && u.Region == req.Region)
                .ToListAsync();

            foreach (var admin in adminsRegion)
            {
                NotificationHelper.Creer(_context, admin.Id,
                    "NouvelleDemandeProprietaire", "Nouvelle demande Propriétaire",
                    $"{demandeur?.Prenom} {demandeur?.Nom} souhaite devenir Propriétaire (région {req.Region}) — {req.DocumentType} joint.",
                    "demandes");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Votre demande et votre pièce d'identité ont été envoyées à l'administration régionale.", data = new DemandeDto(demande, null) });
        }

        // ─── GET /api/demandesproprietaire/document/{filename} ────────────────
        [HttpGet("document/{filename}")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public IActionResult GetDocument(string filename)
        {
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
                return BadRequest();

            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Demandes", filename);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var contentType = Path.GetExtension(filename).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _      => "image/jpeg",
            };
            return PhysicalFile(filePath, contentType);
        }

        // ─── GET /api/demandesproprietaire/mine ───────────────────────────────
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

        // ─── GET /api/demandesproprietaire ────────────────────────────────────
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
                var dateVal = await _context.DemandesProprietaire
                    .Where(d => d.UserId == p.Id && d.Statut == "Validée")
                    .OrderByDescending(d => d.TraiteLe)
                    .Select(d => d.TraiteLe)
                    .FirstOrDefaultAsync();
                data.Add(new ProprietaireDto(p, nbBiens, dateVal));
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
                if (demande.Region != moi!.Region) return Forbid();
            }

            demande.Statut           = "Validée";
            demande.TraiteParAdminId = CurrentUserId();
            demande.TraiteLe         = DateTime.UtcNow;
            demande.User!.Role       = "Proprietaire";
            demande.User.Region      = demande.Region;

            // Marquer le KYC comme approuvé
            demande.User.KycStatut       = "Approuve";
            demande.User.KycDocumentType = demande.DocumentType;
            demande.User.KycDocumentPath = demande.DocumentPath;
            demande.User.KycSoumisAt     = demande.CreatedAt;

            NotificationHelper.Creer(_context, demande.UserId,
                "DemandeValidee", "Demande Propriétaire validée",
                "Votre demande et votre pièce d'identité ont été validées ! Vous pouvez maintenant publier des annonces.",
                "devenir-proprietaire");

            await _context.SaveChangesAsync();
            _ = _email.SendDemandeProprietaireValideeAsync(demande.User!.Email, demande.User.Prenom);

            return Ok(new { success = true, message = $"{demande.User.Prenom} {demande.User.Nom} est maintenant Propriétaire." });
        }

        // ─── PUT /api/demandesproprietaire/{id}/rejeter ───────────────────────
        [HttpPut("{id:int}/rejeter")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Rejeter(int id, [FromBody] RejeterDemandeRequest req)
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
                if (demande.Region != moi!.Region) return Forbid();
            }

            demande.Statut           = "Refusée";
            demande.TraiteParAdminId = CurrentUserId();
            demande.TraiteLe         = DateTime.UtcNow;

            if (demande.User != null)
            {
                demande.User.KycStatut  = "Rejete";
                demande.User.KycRemarque = req.Motif?.Trim();
            }

            var motifMsg = string.IsNullOrWhiteSpace(req.Motif)
                ? "Votre demande pour devenir Propriétaire a été refusée. Vous pouvez en soumettre une nouvelle."
                : $"Votre demande a été refusée : {req.Motif}. Vous pouvez en soumettre une nouvelle avec un document conforme.";

            NotificationHelper.Creer(_context, demande.UserId,
                "DemandeRefusee", "Demande Propriétaire refusée", motifMsg, "devenir-proprietaire");

            await _context.SaveChangesAsync();
            if (demande.User != null)
                _ = _email.SendDemandeProprietaireRefuseeAsync(demande.User.Email, demande.User.Prenom, req.Motif);

            return Ok(new { success = true, message = "Demande refusée." });
        }
    }

    // ─── DTOs & REQUEST MODELS ────────────────────────────────────────────────
    public class DemandeDto
    {
        public int       Id                 { get; set; }
        public int       UserId             { get; set; }
        public string?   Prenom             { get; set; }
        public string?   Nom                { get; set; }
        public string?   Email              { get; set; }
        public string    Region             { get; set; } = string.Empty;
        public string?   Message            { get; set; }
        public string    Statut             { get; set; } = string.Empty;
        public string?   DocumentType       { get; set; }
        public string?   DocumentPath       { get; set; }
        public string?   SelfieDocumentPath { get; set; }
        public DateTime  CreatedAt          { get; set; }

        public DemandeDto(DemandeProprietaire d, User? user)
        {
            Id                 = d.Id;
            UserId             = d.UserId;
            Prenom             = user?.Prenom;
            Nom                = user?.Nom;
            Email              = user?.Email;
            Region             = d.Region;
            Message            = d.Message;
            Statut             = d.Statut;
            DocumentType       = d.DocumentType;
            DocumentPath       = d.DocumentPath;
            SelfieDocumentPath = d.SelfieDocumentPath;
            CreatedAt          = d.CreatedAt;
        }
    }

    public class ProprietaireDto
    {
        public int       Id             { get; set; }
        public string    Prenom         { get; set; } = string.Empty;
        public string    Nom            { get; set; } = string.Empty;
        public string    Email          { get; set; } = string.Empty;
        public string?   Region         { get; set; }
        public string?   PhotoUrl       { get; set; }
        public int       NbBiensPublies { get; set; }
        public DateTime? DateValidation { get; set; }
        public bool      EstBloque      { get; set; }

        public ProprietaireDto(User u, int nbBiens, DateTime? dateValidation)
        {
            Id             = u.Id;
            Prenom         = u.Prenom;
            Nom            = u.Nom;
            Email          = u.Email;
            Region         = u.Region;
            PhotoUrl       = u.PhotoUrl;
            NbBiensPublies = nbBiens;
            DateValidation = dateValidation;
            EstBloque      = u.EstBloque;
        }
    }

    public class CreateDemandeRequest
    {
        public string     Region       { get; set; } = string.Empty;
        public string?    Message      { get; set; }
        public string     DocumentType { get; set; } = string.Empty;
        public IFormFile? Document     { get; set; }
        public IFormFile? Selfie       { get; set; }
    }

    public class RejeterDemandeRequest
    {
        public string? Motif { get; set; }
    }
}
