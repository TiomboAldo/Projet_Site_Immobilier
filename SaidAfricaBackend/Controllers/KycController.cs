using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KycController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment  _env;

        public KycController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env     = env;
        }

        private int     CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private string? CurrentRole()   => User.FindFirst(ClaimTypes.Role)?.Value;
        private bool    IsAdmin()       => CurrentRole() is "AdminRegion" or "AdminPays" or "DirecteurProjet";

        // ─── GET /api/kyc/statut  — propriétaire consulte son statut ─────────
        [HttpGet("statut")]
        [Authorize]
        public async Task<IActionResult> GetStatut()
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();
            return Ok(new
            {
                success = true,
                data = new
                {
                    kycStatut       = user.KycStatut,
                    kycDocumentType = user.KycDocumentType,
                    kycSoumisAt     = user.KycSoumisAt,
                    kycRemarque     = user.KycRemarque,
                }
            });
        }

        // ─── POST /api/kyc/soumettre  — upload du document d'identité ────────
        [HttpPost("soumettre")]
        [Authorize(Roles = "Proprietaire,UserIndep")]
        public async Task<IActionResult> Soumettre([FromForm] SoumettreKycRequest req)
        {
            if (req.Document == null || req.Document.Length == 0)
                return BadRequest(new { success = false, message = "Veuillez joindre un document." });

            if (req.Document.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Le fichier ne doit pas dépasser 5 Mo." });

            var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
            if (!allowed.Contains(req.Document.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Format accepté : JPG, PNG ou PDF." });

            if (req.TypeDocument is not ("CNI" or "Passeport"))
                return BadRequest(new { success = false, message = "Type de document invalide." });

            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();

            if (user.KycStatut == "Approuve")
                return BadRequest(new { success = false, message = "Votre identité est déjà vérifiée." });

            // Sauvegarde du fichier
            var ext       = Path.GetExtension(req.Document.FileName);
            var fileName  = $"{Guid.NewGuid()}{ext}";
            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Kyc");
            Directory.CreateDirectory(uploadDir);
            var filePath  = Path.Combine(uploadDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
                await req.Document.CopyToAsync(stream);

            // Suppression de l'ancien document
            if (!string.IsNullOrEmpty(user.KycDocumentPath))
            {
                var old = Path.Combine(uploadDir, user.KycDocumentPath);
                if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            }

            user.KycStatut       = "EnAttente";
            user.KycDocumentType = req.TypeDocument;
            user.KycDocumentPath = fileName;
            user.KycSoumisAt     = DateTime.UtcNow;
            user.KycRemarque     = null;

            // Notifier les admins de la même région
            var admins = await _context.Users
                .Where(u => u.Role == "AdminRegion" && u.Region == user.Region)
                .ToListAsync();

            foreach (var admin in admins)
                NotificationHelper.Creer(_context, admin.Id, "kyc",
                    "Nouvelle vérification KYC",
                    $"{user.Prenom} {user.Nom} a soumis son {req.TypeDocument} pour vérification.",
                    "kyc");

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Document soumis. Vérification en cours (48h max)." });
        }

        // ─── GET /api/kyc/demandes  — admin voit les demandes ────────────────
        [HttpGet("demandes")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetDemandes([FromQuery] string? statut = "EnAttente")
        {
            var query = _context.Users
                .Where(u => u.KycStatut != "NonSoumis")
                .AsQueryable();

            if (CurrentRole() == "AdminRegion")
            {
                var admin = await _context.Users.FindAsync(CurrentUserId());
                query = query.Where(u => u.Region == admin!.Region);
            }

            if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                query = query.Where(u => u.KycStatut == statut);

            var demandes = await query
                .OrderByDescending(u => u.KycSoumisAt)
                .Select(u => new KycDemandeDto
                {
                    UserId          = u.Id,
                    Prenom          = u.Prenom,
                    Nom             = u.Nom,
                    Email           = u.Email,
                    Region          = u.Region,
                    KycStatut       = u.KycStatut,
                    KycDocumentType = u.KycDocumentType,
                    KycDocumentPath = u.KycDocumentPath,
                    KycSoumisAt     = u.KycSoumisAt,
                    KycRemarque     = u.KycRemarque,
                })
                .ToListAsync();

            return Ok(new { success = true, data = demandes });
        }

        // ─── GET /api/kyc/document/{filename}  — admin visualise le fichier ──
        [HttpGet("document/{filename}")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public IActionResult GetDocument(string filename)
        {
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
                return BadRequest();

            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Kyc", filename);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var contentType = Path.GetExtension(filename).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _      => "image/jpeg",
            };

            return PhysicalFile(filePath, contentType);
        }

        // ─── PUT /api/kyc/{userId}/valider  — admin approuve ─────────────────
        [HttpPut("{userId}/valider")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Valider(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (user.KycStatut != "EnAttente")
                return BadRequest(new { success = false, message = "Aucune demande en attente." });

            if (CurrentRole() == "AdminRegion")
            {
                var admin = await _context.Users.FindAsync(CurrentUserId());
                if (admin?.Region != user.Region) return Forbid();
            }

            user.KycStatut  = "Approuve";
            user.KycRemarque = null;

            NotificationHelper.Creer(_context, userId, "kyc",
                "Identité vérifiée",
                "Votre document a été validé. Vous pouvez désormais publier des annonces sur Said Africa.",
                "kyc");

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "KYC validé avec succès." });
        }

        // ─── PUT /api/kyc/{userId}/rejeter  — admin refuse avec motif ────────
        [HttpPut("{userId}/rejeter")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Rejeter(int userId, [FromBody] RejeterKycRequest req)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();
            if (user.KycStatut != "EnAttente")
                return BadRequest(new { success = false, message = "Aucune demande en attente." });

            if (CurrentRole() == "AdminRegion")
            {
                var admin = await _context.Users.FindAsync(CurrentUserId());
                if (admin?.Region != user.Region) return Forbid();
            }

            user.KycStatut   = "Rejete";
            user.KycRemarque = req.Remarque?.Trim();

            NotificationHelper.Creer(_context, userId, "kyc",
                "Document refusé",
                $"Votre document a été refusé : {req.Remarque}. Veuillez en soumettre un nouveau.",
                "kyc");

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "KYC refusé." });
        }
    }

    public class SoumettreKycRequest
    {
        public string       TypeDocument { get; set; } = string.Empty;
        public IFormFile?   Document     { get; set; }
    }

    public class RejeterKycRequest
    {
        public string Remarque { get; set; } = string.Empty;
    }

    public class KycDemandeDto
    {
        public int       UserId          { get; set; }
        public string    Prenom          { get; set; } = string.Empty;
        public string    Nom             { get; set; } = string.Empty;
        public string    Email           { get; set; } = string.Empty;
        public string?   Region          { get; set; }
        public string    KycStatut       { get; set; } = string.Empty;
        public string?   KycDocumentType { get; set; }
        public string?   KycDocumentPath { get; set; }
        public DateTime? KycSoumisAt     { get; set; }
        public string?   KycRemarque     { get; set; }
    }
}
