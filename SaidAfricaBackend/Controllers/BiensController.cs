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
        private readonly IWebHostEnvironment  _env;

        public BiensController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env     = env;
        }

        private int     CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private string? CurrentRole()   => User.FindFirst(ClaimTypes.Role)?.Value;
        private bool    IsAdmin()       => CurrentRole() is "AdminRegion" or "AdminPays" or "DirecteurProjet";

        private async Task<bool> IsAdminForBienAsync(Bien bien)
        {
            var role = CurrentRole();
            if (role is "AdminPays" or "DirecteurProjet") return true;
            if (role != "AdminRegion") return false;

            var admin   = await _context.Users.FindAsync(CurrentUserId());
            var proprio = await _context.Users.FindAsync(bien.ProprietaireId);
            return admin?.Region != null && admin.Region == proprio?.Region;
        }

        // ─── GET /api/biens ───────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? type,
            [FromQuery] string? statut,
            [FromQuery] string? standing,
            [FromQuery] string? q)
        {
          try {
            var query = _context.Biens
                .Where(b => b.EstDisponible && b.StatutPublication == "Valide")
                .AsQueryable();

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

            var biensList = await query.OrderByDescending(b => b.DateAjout).ToListAsync();
            var bienIds   = biensList.Select(b => b.Id).ToList();

            var likesDict = await _context.BienLikes
                .Where(l => bienIds.Contains(l.BienId))
                .GroupBy(l => l.BienId)
                .Select(g => new { BienId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BienId, x => x.Count);

            int? callerId = User.Identity?.IsAuthenticated == true ? CurrentUserId() : null;
            var likedByMe = callerId.HasValue
                ? (await _context.BienLikes
                    .Where(l => bienIds.Contains(l.BienId) && l.UserId == callerId.Value)
                    .Select(l => l.BienId)
                    .ToListAsync()).ToHashSet()
                : new HashSet<int>();

            var biens = biensList.Select(b => new BienDto(b,
                likesDict.GetValueOrDefault(b.Id, 0),
                likedByMe.Contains(b.Id))).ToList();

            return Ok(new { success = true, data = biens });
          }
          catch (Exception ex)
          {
              return StatusCode(500, new { success = false, message = ex.Message, detail = ex.InnerException?.Message ?? "" });
          }
        }

        // ─── GET /api/biens/debug ─────────────────────────────────────────────
        [HttpGet("debug")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugBiens()
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            int rawCount;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM `biens` WHERE EstDisponible = 1 AND StatutPublication = 'Valide'";
                rawCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var efTotal      = await _context.Biens.CountAsync();
            var efValide     = await _context.Biens.CountAsync(b => b.StatutPublication == "Valide");
            var efDisponible = await _context.Biens.CountAsync(b => b.EstDisponible);
            var efBoth       = await _context.Biens.CountAsync(b => b.EstDisponible && b.StatutPublication == "Valide");

            var samples = await _context.Biens
                .Select(b => new { b.Id, b.StatutPublication, b.EstDisponible })
                .Take(15)
                .ToListAsync();

            var connStr = _context.Database.GetConnectionString() ?? "";
            var dbName  = connStr.Split(';')
                .FirstOrDefault(p => p.TrimStart().StartsWith("database=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=').LastOrDefault() ?? "unknown";

            return Ok(new { rawCount, efTotal, efValide, efDisponible, efBoth, samples, dbName });
        }

        // ─── GET /api/biens/{id} ──────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bien = await _context.Biens
                .Include(b => b.Proprietaire)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (User.Identity?.IsAuthenticated == true)
            {
                int uid = CurrentUserId();
                bool alreadyViewed = await _context.BienVues.AnyAsync(v => v.BienId == id && v.UserId == uid);
                if (!alreadyViewed)
                {
                    _context.BienVues.Add(new BienVue { BienId = id, UserId = uid });
                    bien.Vues += 1;
                }
            }
            else
            {
                bien.Vues += 1;
            }
            await _context.SaveChangesAsync();

            int likes = await _context.BienLikes.CountAsync(l => l.BienId == id);
            bool estLikeParMoi = User.Identity?.IsAuthenticated == true
                && await _context.BienLikes.AnyAsync(l => l.BienId == id && l.UserId == CurrentUserId());

            return Ok(new { success = true, data = new BienDto(bien, likes, estLikeParMoi) });
        }

        // ─── GET /api/biens/region  (admin : tous les biens de la région) ─────
        [HttpGet("region")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetRegion()
        {
            var query = _context.Biens.Include(b => b.Proprietaire).AsQueryable();

            if (CurrentRole() == "AdminRegion")
            {
                var admin = await _context.Users.FindAsync(CurrentUserId());
                query = query.Where(b => b.Proprietaire != null && b.Proprietaire.Region == admin!.Region);
            }

            var biensList = await query.OrderByDescending(b => b.DateAjout).ToListAsync();
            var bienIds   = biensList.Select(b => b.Id).ToList();
            var likesDict = await _context.BienLikes
                .Where(l => bienIds.Contains(l.BienId))
                .GroupBy(l => l.BienId)
                .Select(g => new { BienId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BienId, x => x.Count);

            return Ok(new { success = true, data = biensList.Select(b => new BienRegionDto(b, likesDict.GetValueOrDefault(b.Id, 0))).ToList() });
        }

        // ─── GET /api/biens/mine ──────────────────────────────────────────────
        [HttpGet("mine")]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetMine()
        {
            var userId = CurrentUserId();

            var biensList = await _context.Biens
                .Where(b => b.ProprietaireId == userId)
                .OrderByDescending(b => b.DateAjout)
                .ToListAsync();

            var bienIds = biensList.Select(b => b.Id).ToList();
            var likesDict = await _context.BienLikes
                .Where(l => bienIds.Contains(l.BienId))
                .GroupBy(l => l.BienId)
                .Select(g => new { BienId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BienId, x => x.Count);

            return Ok(new { success = true, data = biensList.Select(b => new BienDto(b, likesDict.GetValueOrDefault(b.Id, 0))).ToList() });
        }

        // ─── GET /api/biens/document/{filename}  (titre foncier — admin) ──────
        [HttpGet("document/{filename}")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public IActionResult GetDocument(string filename)
        {
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
                return BadRequest();

            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", "Biens", filename);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var contentType = Path.GetExtension(filename).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _      => "image/jpeg",
            };
            return PhysicalFile(filePath, contentType);
        }

        // ─── PUT /api/biens/{id}  (modifier son annonce) ─────────────────────
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBienRequest req)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId() && !await IsAdminForBienAsync(bien))
                return Forbid();

            bien.Titre        = req.Titre;
            bien.Type         = req.Type;
            bien.Statut       = req.Statut;
            bien.Prix         = req.Prix;
            bien.Chambres     = req.Chambres;
            bien.SallesDeBain = req.SallesDeBain;
            bien.Surface      = req.Surface;
            bien.Localisation = req.Localisation;
            bien.Description  = req.Description;
            bien.ImageUrl     = req.ImageUrl;
            bien.GalerieUrls  = req.GalerieUrls;
            bien.Equipements  = req.Equipements;
            bien.Standing     = req.Standing;
            bien.Latitude          = req.Latitude;
            bien.Longitude         = req.Longitude;
            bien.FraisVisite       = req.FraisVisite;
            bien.StatutCivil       = req.StatutCivil;
            bien.RegimeMatrimonial = req.RegimeMatrimonial;
            bien.ADesEnfants       = req.ADesEnfants;

            string message;
            if (IsAdmin())
            {
                bool etaitDesactive = !bien.EstDisponible;
                bien.EstDisponible = req.EstDisponible;

                if (bien.ProprietaireId != null && etaitDesactive && req.EstDisponible)
                    NotificationHelper.Creer(_context,
                        bien.ProprietaireId.Value,
                        "moderation",
                        "Votre annonce a été réactivée",
                        $"Votre bien « {bien.Titre} » a été réactivé par l'administration régionale.",
                        "mes-biens");

                message = "Bien mis à jour avec succès.";
            }
            else
            {
                // Modification par le propriétaire → retour en file de modération
                bien.StatutPublication = "En attente";
                bien.EstDisponible     = false;
                bien.ValideParAdminId  = null;
                bien.ValideLe          = null;
                bien.MotifsRejet       = null;

                // Notifier l'admin régional responsable de la région du propriétaire
                if (bien.ProprietaireId.HasValue)
                {
                    var prop = await _context.Users.FindAsync(bien.ProprietaireId.Value);
                    if (prop != null)
                    {
                        var admins = await _context.Users
                            .Where(u => u.Role == "AdminRegion" || u.Role == "AdminPays" || u.Role == "DirecteurProjet")
                            .ToListAsync();

                        foreach (var admin in admins)
                            NotificationHelper.Creer(_context, admin.Id,
                                "moderation",
                                "Bien modifié — revalidation requise",
                                $"{prop.Prenom} {prop.Nom} a modifié « {bien.Titre} ». Veuillez revalider avant remise en ligne.",
                                "biens");
                    }
                }

                message = "Modifications sauvegardées. Votre annonce est en attente de revalidation par l'admin régional.";
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message, data = new BienDto(bien) });
        }

        // ─── PUT /api/biens/{id}/valider  (AdminRegion valide) ───────────────
        [HttpPut("{id:int}/valider")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Valider(int id)
        {
            var bien = await _context.Biens
                .Include(b => b.Proprietaire)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (!await IsAdminForBienAsync(bien))
                return Forbid();

            if (bien.StatutPublication == "Valide")
                return BadRequest(new { success = false, message = "Ce bien est déjà validé." });

            bien.StatutPublication = "Valide";
            bien.EstDisponible     = true;
            bien.MotifsRejet       = null;
            bien.ValideParAdminId  = CurrentUserId();
            bien.ValideLe          = DateTime.UtcNow;

            if (bien.ProprietaireId != null)
            {
                NotificationHelper.Creer(_context,
                    bien.ProprietaireId.Value,
                    "moderation",
                    "Annonce validée !",
                    $"Votre bien « {bien.Titre} » a été vérifié sur le terrain et validé. Il est maintenant visible sur la plateforme.",
                    "mes-biens");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Bien « {bien.Titre} » validé et mis en ligne." });
        }

        // ─── PUT /api/biens/{id}/rejeter  (AdminRegion rejette) ──────────────
        [HttpPut("{id:int}/rejeter")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Rejeter(int id, [FromBody] RejeterBienRequest req)
        {
            var bien = await _context.Biens
                .Include(b => b.Proprietaire)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (!await IsAdminForBienAsync(bien))
                return Forbid();

            bien.StatutPublication = "Rejetée";
            bien.EstDisponible     = false;
            bien.MotifsRejet       = req.Motif?.Trim();

            if (bien.ProprietaireId != null)
            {
                var motifMsg = string.IsNullOrWhiteSpace(req.Motif)
                    ? $"Votre bien « {bien.Titre} » n'a pas pu être validé. Corrigez les informations et resoumettez."
                    : $"Votre bien « {bien.Titre} » a été rejeté : {req.Motif}. Corrigez et resoumettez votre annonce.";

                NotificationHelper.Creer(_context,
                    bien.ProprietaireId.Value,
                    "moderation",
                    "Annonce rejetée",
                    motifMsg,
                    "mes-biens");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Annonce rejetée." });
        }

        // ─── PUT /api/biens/{id}/resoumettre  (Propriétaire remet en modération) ─
        [HttpPut("{id:int}/resoumettre")]
        [Authorize(Roles = "Proprietaire,UserIndep")]
        public async Task<IActionResult> Resoumettre(int id)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId())
                return Forbid();

            if (bien.StatutPublication != "Rejetée")
                return BadRequest(new { success = false, message = "Seuls les biens rejetés peuvent être resoumis." });

            bien.StatutPublication = "En attente";
            bien.EstDisponible     = false;
            bien.MotifsRejet       = null;

            var proprio = await _context.Users.FindAsync(CurrentUserId());
            var admins2 = await _context.Users
                .Where(u => u.Role == "AdminRegion" || u.Role == "AdminPays" || u.Role == "DirecteurProjet")
                .ToListAsync();

            var nomProprio2 = proprio != null ? $"{proprio.Prenom} {proprio.Nom}" : "Un utilisateur";
            foreach (var admin in admins2)
            {
                NotificationHelper.Creer(_context,
                    admin.Id,
                    "nouvelle-annonce",
                    "Bien resoumis pour validation",
                    $"{nomProprio2} a resoumis « {bien.Titre} » pour validation.",
                    "biens");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Bien resoumis à la modération." });
        }

        // ─── DELETE /api/biens/{id} ───────────────────────────────────────────
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Deactivate(int id)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId() && !await IsAdminForBienAsync(bien))
                return Forbid();

            bien.EstDisponible = false;

            if (IsAdmin() && bien.ProprietaireId != null)
            {
                NotificationHelper.Creer(_context,
                    bien.ProprietaireId.Value,
                    "moderation",
                    "Votre annonce a été désactivée",
                    $"Votre bien « {bien.Titre} » a été désactivé par l'administration régionale.",
                    "mes-biens");
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Annonce retirée." });
        }

        // ─── POST /api/biens/upload-titre-foncier ────────────────────────────
        [HttpPost("upload-titre-foncier")]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> UploadTitreFoncier(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Aucun fichier envoyé." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Le titre foncier ne doit pas dépasser 10 Mo." });

            var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
            if (!allowed.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Format accepté : JPG, PNG ou PDF." });

            var dir = Path.Combine(_env.ContentRootPath, "Uploads", "Biens");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName);
            var filename = $"tf_{Guid.NewGuid()}{ext}";
            using var s = System.IO.File.Create(Path.Combine(dir, filename));
            await file.CopyToAsync(s);

            return Ok(new { success = true, filename });
        }

        // ─── POST /api/biens/upload-document ─────────────────────────────────
        // Upload générique pour CertificatPropriete, DossierTechnique, PermisBatir, etc.
        [HttpPost("upload-document")]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> UploadDocument(IFormFile? file, [FromQuery] string prefix = "doc")
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Aucun fichier envoyé." });
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Fichier trop volumineux (max 10 Mo)." });

            var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
            if (!allowed.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Format accepté : JPG, PNG ou PDF." });

            var safePrefix = System.Text.RegularExpressions.Regex.Replace(prefix, @"[^a-zA-Z0-9_-]", "");
            var dir = Path.Combine(_env.ContentRootPath, "Uploads", "Biens");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName);
            var filename = $"{safePrefix}_{Guid.NewGuid()}{ext}";
            using var s = System.IO.File.Create(Path.Combine(dir, filename));
            await file.CopyToAsync(s);

            return Ok(new { success = true, filename });
        }

        // ─── PUT /api/biens/{id}/checklist ────────────────────────────────────
        [HttpPut("{id:int}/checklist")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> SaveChecklist(int id, [FromBody] ChecklistRequest req)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null) return NotFound(new { success = false });

            bien.DocumentsVerifies = req.DocumentsVerifies;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── POST /api/biens ──────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> Create([FromBody] CreateBienRequest req)
        {
            var proprietaireId = CurrentUserId();
            var proprio = await _context.Users.FindAsync(proprietaireId);

            if (CurrentRole() is "Proprietaire" or "UserIndep" && proprio?.KycStatut != "Approuve")
                return BadRequest(new
                {
                    success = false,
                    message = "Votre identité doit être vérifiée avant de publier une annonce. Rendez-vous dans la section « Vérification KYC ».",
                    kycRequired = true
                });

            if (req.Statut?.ToLower() == "vente"
                && CurrentRole() is "Proprietaire" or "UserIndep"
                && string.IsNullOrWhiteSpace(req.TitreFoncierPath))
            {
                return BadRequest(new { success = false, message = "Le titre foncier est obligatoire pour les biens en vente." });
            }

            // Les admins publient directement sans passer par la modération
            bool isAdmin = CurrentRole() is "AdminRegion" or "AdminPays" or "DirecteurProjet";

            var bien = new Bien
            {
                Titre             = req.Titre,
                Type              = req.Type,
                Statut            = req.Statut,
                Prix              = req.Prix,
                Chambres          = req.Chambres,
                SallesDeBain      = req.SallesDeBain,
                Surface           = req.Surface,
                Localisation      = req.Localisation,
                Description       = req.Description,
                ImageUrl          = req.ImageUrl,
                GalerieUrls       = req.GalerieUrls,
                Equipements       = req.Equipements,
                Standing          = req.Standing,
                Latitude          = req.Latitude,
                Longitude         = req.Longitude,
                ProprietaireId    = proprietaireId,
                EstDisponible          = isAdmin,
                StatutPublication      = isAdmin ? "Valide" : "En attente",
                ValideParAdminId       = isAdmin ? proprietaireId : null,
                ValideLe               = isAdmin ? DateTime.UtcNow : null,
                TitreFoncierPath       = req.TitreFoncierPath,
                CertificatPropriete    = req.CertificatPropriete,
                StatutCivil            = req.StatutCivil,
                RegimeMatrimonial      = req.RegimeMatrimonial,
                ADesEnfants            = req.ADesEnfants,
                DossierTechnique       = req.DossierTechnique,
                PermisBatir            = req.PermisBatir,
                PlanBatiment           = req.PlanBatiment,
                DossierCalculTechnique = req.DossierCalculTechnique,
                FraisVisite            = req.FraisVisite,
            };

            _context.Biens.Add(bien);

            if (!isAdmin)
            {
                // Notifier tous les admins sans filtre de région
                var admins = await _context.Users
                    .Where(u => u.Role == "AdminRegion" || u.Role == "AdminPays" || u.Role == "DirecteurProjet")
                    .ToListAsync();

                var nomProprio = proprio != null ? $"{proprio.Prenom} {proprio.Nom}" : "Un utilisateur";
                foreach (var admin in admins)
                {
                    NotificationHelper.Creer(_context,
                        admin.Id,
                        "nouvelle-annonce",
                        "Nouveau bien à valider",
                        $"{nomProprio} a publié « {req.Titre} » à {req.Localisation} — en attente de validation.",
                        "biens");
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = isAdmin
                    ? $"Annonce publiée et disponible sur la plateforme."
                    : "Votre annonce est soumise. Elle sera visible après validation par l'admin régional.",
                data = new BienDto(bien)
            });
        }

        // ─── POST /api/biens/{id}/like  (toggle like — authentifié) ──────────
        [HttpPost("{id:int}/like")]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int id)
        {
            var bien = await _context.Biens.FindAsync(id);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            var userId   = CurrentUserId();
            var existing = await _context.BienLikes
                .FirstOrDefaultAsync(l => l.BienId == id && l.UserId == userId);

            bool liked;
            if (existing != null)
            {
                _context.BienLikes.Remove(existing);
                liked = false;
            }
            else
            {
                _context.BienLikes.Add(new BienLike { BienId = id, UserId = userId });
                liked = true;
            }

            await _context.SaveChangesAsync();
            int total = await _context.BienLikes.CountAsync(l => l.BienId == id);

            return Ok(new { success = true, liked, total });
        }
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────────
    public class BienDto
    {
        public int      Id                { get; set; }
        public string   Titre             { get; set; }
        public string   Type              { get; set; }
        public string   Statut            { get; set; }
        public string   Prix              { get; set; }
        public int      Chambres          { get; set; }
        public int      SallesDeBain      { get; set; }
        public int      Surface           { get; set; }
        public string   Localisation      { get; set; }
        public string   Description       { get; set; }
        public string   ImageUrl          { get; set; }
        public string   Standing          { get; set; }
        public bool     EstDisponible     { get; set; }
        public string   StatutPublication { get; set; }
        public string?  MotifsRejet       { get; set; }
        public string?  TitreFoncierPath  { get; set; }
        public DateTime DateAjout         { get; set; }
        public int      Vues              { get; set; }
        public int      Likes             { get; set; }
        public bool     EstLikeParMoi     { get; set; }
        public int?     ProprietaireId         { get; set; }
        public string?  TelephoneProprietaire  { get; set; }
        public bool     VendeurCertifie        { get; set; }
        public double?  Latitude               { get; set; }
        public double?  Longitude              { get; set; }
        public List<string> Galerie            { get; set; }
        public List<string> Equipements        { get; set; }
        // Documents spécifiques Terrain
        public string?  CertificatPropriete    { get; set; }
        public string?  StatutCivil            { get; set; }
        public string?  RegimeMatrimonial      { get; set; }
        public bool?    ADesEnfants            { get; set; }
        public string?  DossierTechnique       { get; set; }
        // Documents spécifiques Immeuble
        public string?  PermisBatir            { get; set; }
        public string?  PlanBatiment           { get; set; }
        public string?  DossierCalculTechnique { get; set; }
        public string?  DocumentsVerifies      { get; set; }
        public int?     FraisVisite            { get; set; }

        public BienDto(Bien b, int likes = 0, bool estLikeParMoi = false)
        {
            Id                    = b.Id;
            Titre                 = b.Titre;
            Type                  = b.Type;
            Statut                = b.Statut;
            Prix                  = b.Prix;
            Chambres              = b.Chambres;
            SallesDeBain          = b.SallesDeBain;
            Surface               = b.Surface;
            Localisation          = b.Localisation;
            Description           = b.Description;
            ImageUrl              = b.ImageUrl;
            Standing              = b.Standing;
            EstDisponible         = b.EstDisponible;
            StatutPublication     = b.StatutPublication;
            MotifsRejet           = b.MotifsRejet;
            TitreFoncierPath      = b.TitreFoncierPath;
            DateAjout             = b.DateAjout;
            Vues                  = b.Vues;
            Likes                 = likes;
            EstLikeParMoi         = estLikeParMoi;
            ProprietaireId        = b.ProprietaireId;
            TelephoneProprietaire = b.Proprietaire?.Telephone;
            VendeurCertifie       = b.Proprietaire?.KycStatut == "Approuve";
            Latitude              = b.Latitude;
            Longitude             = b.Longitude;
            Galerie           = string.IsNullOrEmpty(b.GalerieUrls)
                                    ? new List<string>()
                                    : b.GalerieUrls.Split('|').ToList();
            Equipements       = string.IsNullOrEmpty(b.Equipements)
                                    ? new List<string>()
                                    : b.Equipements.Split('|').ToList();
            CertificatPropriete    = b.CertificatPropriete;
            StatutCivil            = b.StatutCivil;
            RegimeMatrimonial      = b.RegimeMatrimonial;
            ADesEnfants            = b.ADesEnfants;
            DossierTechnique       = b.DossierTechnique;
            PermisBatir            = b.PermisBatir;
            PlanBatiment           = b.PlanBatiment;
            DossierCalculTechnique = b.DossierCalculTechnique;
            DocumentsVerifies      = b.DocumentsVerifies;
            FraisVisite            = b.FraisVisite;
        }
    }

    public class BienRegionDto : BienDto
    {
        public string? PrenomProprietaire { get; set; }
        public string? NomProprietaire    { get; set; }
        public string? ProprietaireRole   { get; set; }

        public BienRegionDto(Bien b, int likes = 0) : base(b, likes)
        {
            PrenomProprietaire = b.Proprietaire?.Prenom;
            NomProprietaire    = b.Proprietaire?.Nom;
            ProprietaireRole   = b.Proprietaire?.Role;
        }
    }

    // ─── REQUEST MODELS ───────────────────────────────────────────────────────
    public class CreateBienRequest
    {
        public string     Titre        { get; set; } = string.Empty;
        public string     Type         { get; set; } = string.Empty;
        public string     Statut       { get; set; } = string.Empty;
        public string     Prix         { get; set; } = string.Empty;
        public int        Chambres     { get; set; }
        public int        SallesDeBain { get; set; }
        public int        Surface      { get; set; }
        public string     Localisation { get; set; } = string.Empty;
        public string     Description  { get; set; } = string.Empty;
        public string     ImageUrl     { get; set; } = string.Empty;
        public string     GalerieUrls  { get; set; } = string.Empty;
        public string     Equipements  { get; set; } = string.Empty;
        public string     Standing     { get; set; } = string.Empty;
        public double?    Latitude     { get; set; }
        public double?    Longitude    { get; set; }
        public string?    TitreFoncierPath     { get; set; }
        // Terrain
        public string?    CertificatPropriete  { get; set; }
        public string?    StatutCivil          { get; set; }
        public string?    RegimeMatrimonial    { get; set; }
        public bool?      ADesEnfants          { get; set; }
        public string?    DossierTechnique     { get; set; }
        // Immeuble
        public string?    PermisBatir           { get; set; }
        public string?    PlanBatiment          { get; set; }
        public string?    DossierCalculTechnique{ get; set; }
        public int?       FraisVisite           { get; set; }
    }

    public class UpdateBienRequest
    {
        public string  Titre         { get; set; } = string.Empty;
        public string  Type          { get; set; } = string.Empty;
        public string  Statut        { get; set; } = string.Empty;
        public string  Prix          { get; set; } = string.Empty;
        public int     Chambres      { get; set; }
        public int     SallesDeBain  { get; set; }
        public int     Surface       { get; set; }
        public string  Localisation  { get; set; } = string.Empty;
        public string  Description   { get; set; } = string.Empty;
        public string  ImageUrl      { get; set; } = string.Empty;
        public string  GalerieUrls   { get; set; } = string.Empty;
        public string  Equipements   { get; set; } = string.Empty;
        public string  Standing      { get; set; } = string.Empty;
        public bool    EstDisponible  { get; set; } = true;
        public double? Latitude      { get; set; }
        public double? Longitude     { get; set; }
        public int?    FraisVisite   { get; set; }
        public string?  StatutCivil        { get; set; }
        public string?  RegimeMatrimonial  { get; set; }
        public bool?    ADesEnfants        { get; set; }
    }

    public class RejeterBienRequest
    {
        public string? Motif { get; set; }
    }

    public class ChecklistRequest
    {
        public string? DocumentsVerifies { get; set; }
    }
}
