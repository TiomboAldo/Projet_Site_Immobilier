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
        // Retourne toutes les régions Cameroun — l'admin du Littoral couvre tout jusqu'à déploiement complet
        [HttpGet("regions-with-admin")]
        [AllowAnonymous]
        public IActionResult GetRegionsWithAdmin()
        {
            var regions = new[]
            {
                "Adamaoua","Centre","Est","Extrême-Nord",
                "Littoral","Nord","Nord-Ouest","Ouest","Sud","Sud-Ouest"
            };
            return Ok(new { success = true, data = regions });
        }

        // ─── POST /api/demandesproprietaire ───────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create([FromBody] CreateDemandeRequest req)
        {
            var userId = CurrentUserId();

            bool dejaEnAttente = await _context.DemandesProprietaire
                .AnyAsync(d => d.UserId == userId && d.Statut == "En attente");

            if (dejaEnAttente)
                return BadRequest(new { success = false, message = "Vous avez déjà une demande en attente." });

            // Validation type de compte professionnel
            var validTypes = new[] { "Proprietaire", "PromoteurImmobilier", "AgenceImmobiliere", "AgentImmobilier" };
            if (!validTypes.Contains(req.TypeCompteProf))
                return BadRequest(new { success = false, message = "Type de compte professionnel invalide." });

            // Validation pièce d'identité (recto)
            if (string.IsNullOrEmpty(req.DocumentB64))
                return BadRequest(new { success = false, message = "Veuillez joindre le recto de votre pièce d'identité." });

            byte[] docBytes;
            try   { docBytes = Convert.FromBase64String(req.DocumentB64); }
            catch { return BadRequest(new { success = false, message = "Fichier CNI recto invalide." }); }
            if (docBytes.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "La pièce d'identité ne doit pas dépasser 5 Mo." });

            var allowedDocExt = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var docExt = (req.DocumentExt ?? ".jpg").ToLowerInvariant();
            if (!allowedDocExt.Contains(docExt))
                return BadRequest(new { success = false, message = "CNI recto : format accepté JPG, PNG ou PDF." });

            if (req.DocumentType is not ("CNI" or "Passeport"))
                return BadRequest(new { success = false, message = "Type de document invalide." });

            // Validation verso CNI (obligatoire uniquement pour CNI)
            byte[]? versoBytes = null;
            if (req.DocumentType == "CNI")
            {
                if (string.IsNullOrEmpty(req.DocumentVersoB64))
                    return BadRequest(new { success = false, message = "Veuillez joindre le verso de votre CNI." });
                try   { versoBytes = Convert.FromBase64String(req.DocumentVersoB64); }
                catch { return BadRequest(new { success = false, message = "Fichier verso invalide." }); }
                if (versoBytes.Length > 5 * 1024 * 1024)
                    return BadRequest(new { success = false, message = "Le verso de la CNI ne doit pas dépasser 5 Mo." });
            }

            // Vérifier qu'au moins un admin régional existe sur la plateforme
            bool adminExiste = await _context.Users.AnyAsync(u => u.Role == "AdminRegion");
            if (!adminExiste)
                return BadRequest(new { success = false, message = "Aucun administrateur régional disponible pour le moment. Contactez le support." });

            // Validation selfie
            if (string.IsNullOrEmpty(req.SelfieB64))
                return BadRequest(new { success = false, message = "Veuillez joindre votre selfie de vérification." });

            byte[] selfieBytes;
            try   { selfieBytes = Convert.FromBase64String(req.SelfieB64); }
            catch { return BadRequest(new { success = false, message = "Selfie invalide." }); }
            if (selfieBytes.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Le selfie ne doit pas dépasser 5 Mo." });

            // Sauvegarde des fichiers
            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Demandes");
            Directory.CreateDirectory(uploadDir);

            var docFileName = $"{Guid.NewGuid()}{docExt}";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadDir, docFileName), docBytes);

            string? versoFileName = null;
            if (versoBytes != null)
            {
                var versoExt = (req.DocumentVersoExt ?? ".jpg").ToLowerInvariant();
                versoFileName = $"{Guid.NewGuid()}{versoExt}";
                await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadDir, versoFileName), versoBytes);
            }

            var selfieFileName = $"{Guid.NewGuid()}.jpg";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadDir, selfieFileName), selfieBytes);

            var typeLabel = req.TypeCompteProf switch
            {
                "PromoteurImmobilier" => "Promoteur Immobilier",
                "AgenceImmobiliere"   => "Agence Immobilière",
                "AgentImmobilier"     => "Agent Immobilier",
                _                     => "Propriétaire",
            };

            var demande = new DemandeProprietaire
            {
                UserId             = userId,
                Region             = req.Region,
                Message            = req.Message,
                DocumentType       = req.DocumentType,
                DocumentPath       = docFileName,
                CNIVersoPath       = versoFileName,
                SelfieDocumentPath = selfieFileName,
                TypeCompteProf     = req.TypeCompteProf,
                NomAgence          = req.NomAgence?.Trim(),
                NIU                = req.NIU?.Trim(),
            };

            _context.DemandesProprietaire.Add(demande);

            var demandeur    = await _context.Users.FindAsync(userId);

            // Admins de la région sélectionnée ; si aucun, on prend tous les admins régionaux disponibles
            var adminsRegion = await _context.Users
                .Where(u => u.Role == "AdminRegion" && u.Region == req.Region)
                .ToListAsync();
            if (adminsRegion.Count == 0)
                adminsRegion = await _context.Users
                    .Where(u => u.Role == "AdminRegion")
                    .ToListAsync();

            foreach (var admin in adminsRegion)
            {
                NotificationHelper.Creer(_context, admin.Id,
                    "NouvelleDemandeProprietaire", "Nouvelle demande Compte Professionnel",
                    $"{demandeur?.Prenom} {demandeur?.Nom} souhaite ouvrir un compte {typeLabel} (région {req.Region}) — {req.DocumentType} joint.",
                    "demandes");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Votre demande a été envoyée à l'administration régionale.", data = new DemandeDto(demande, null) });
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
                    .Where(d => d.UserId == p.Id && d.Statut == "Valide")
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

            var typeLabel = demande.TypeCompteProf switch
            {
                "PromoteurImmobilier" => "Promoteur Immobilier",
                "AgenceImmobiliere"   => "Agence Immobilière",
                "AgentImmobilier"     => "Agent Immobilier",
                _                     => "Propriétaire",
            };

            demande.Statut           = "Valide";
            demande.TraiteParAdminId = CurrentUserId();
            demande.TraiteLe         = DateTime.UtcNow;
            demande.User!.Role       = "Proprietaire";
            demande.User.Region      = demande.Region;

            // Copier les infos du compte professionnel sur l'utilisateur
            demande.User.TypeCompteProf = demande.TypeCompteProf;
            demande.User.NIU            = demande.NIU;
            demande.User.NomAgence      = demande.NomAgence;

            // Marquer le KYC comme approuvé
            demande.User.KycStatut       = "Approuve";
            demande.User.KycDocumentType = demande.DocumentType;
            demande.User.KycDocumentPath = demande.DocumentPath;
            demande.User.KycSoumisAt     = demande.CreatedAt;

            NotificationHelper.Creer(_context, demande.UserId,
                "DemandeValidee", "Compte Professionnel validé",
                $"Votre demande a été validée ! Vous êtes maintenant {typeLabel} et pouvez publier des annonces.",
                "devenir-proprietaire");

            await _context.SaveChangesAsync();
            _ = _email.SendDemandeProprietaireValideeAsync(demande.User!.Email, demande.User.Prenom);

            return Ok(new { success = true, message = $"{demande.User.Prenom} {demande.User.Nom} est maintenant {typeLabel}." });
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
        public string?   CNIVersoPath       { get; set; }
        public string?   SelfieDocumentPath { get; set; }
        public string    TypeCompteProf     { get; set; } = "Proprietaire";
        public string?   NomAgence          { get; set; }
        public string?   NIU                { get; set; }
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
            CNIVersoPath       = d.CNIVersoPath;
            SelfieDocumentPath = d.SelfieDocumentPath;
            TypeCompteProf     = d.TypeCompteProf;
            NomAgence          = d.NomAgence;
            NIU                = d.NIU;
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
        public string?   TypeCompteProf { get; set; } 
        public string?   NIU            { get; set; }
        public string?   NomAgence      { get; set; }

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
            TypeCompteProf = u.TypeCompteProf;
            NIU            = u.NIU;
            NomAgence      = u.NomAgence;
        }
    }

    public class CreateDemandeRequest
    {
        public string  Region            { get; set; } = string.Empty;
        public string? Message           { get; set; }
        public string  DocumentType      { get; set; } = string.Empty;
        public string  DocumentB64       { get; set; } = string.Empty;
        public string  DocumentExt       { get; set; } = ".jpg";
        public string? DocumentVersoB64  { get; set; }
        public string? DocumentVersoExt  { get; set; }
        public string  SelfieB64         { get; set; } = string.Empty;
        public string  TypeCompteProf    { get; set; } = "Proprietaire";
        public string? NomAgence         { get; set; }
        public string? NIU               { get; set; }
    }

    public class RejeterDemandeRequest
    {
        public string? Motif { get; set; }
    }
}
