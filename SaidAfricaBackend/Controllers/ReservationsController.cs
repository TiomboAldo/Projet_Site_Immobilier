using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private bool IsAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role is "AdminRegion" or "AdminPays" or "DirecteurProjet";
        }

        // ─── POST /api/reservations ───────────────────────────────────────────
        // Créer une demande de visite (pour l'utilisateur authentifié)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateReservationRequest req)
        {
            var userId = CurrentUserId();

            // Vérifier que le bien existe
            var bien = await _context.Biens.FindAsync(req.BienId);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            // Vérifier que l'utilisateur existe
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "Utilisateur introuvable." });

            var reservation = new Reservation
            {
                UserId     = userId,
                BienId     = req.BienId,
                Prenom     = req.Prenom,
                Nom        = req.Nom,
                Email      = req.Email,
                Telephone  = req.Telephone,
                Lieu       = req.Lieu,
                DateVisite = req.DateVisite,
                Message    = req.Message ?? string.Empty,
                Statut     = "En attente"
            };

            _context.Reservations.Add(reservation);

            if (bien.ProprietaireId.HasValue)
            {
                NotificationHelper.Creer(_context, bien.ProprietaireId.Value,
                    "NouvelleReservation", "Nouvelle réservation reçue",
                    $"{req.Prenom} {req.Nom} a demandé une visite pour « {bien.Titre} ».",
                    "reservations");
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Votre demande de visite pour « {bien.Titre} » a été enregistrée. Nous vous contacterons sous 24h.",
                data = new ReservationDto(reservation, bien.Titre)
            });
        }

        // ─── GET /api/reservations/user/{userId} ──────────────────────────────
        // Récupérer toutes les réservations d'un utilisateur
        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetByUser(int userId)
        {
            if (userId != CurrentUserId() && !IsAdmin())
                return Forbid();

            var reservations = await _context.Reservations
                .Where(r => r.UserId == userId)
                .Include(r => r.Bien)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReservationDto(r, r.Bien!.Titre))
                .ToListAsync();

            return Ok(new { success = true, data = reservations });
        }

        // ─── GET /api/reservations/proprietaire ───────────────────────────────
        // Réservations reçues sur les biens du propriétaire connecté
        [HttpGet("proprietaire")]
        [Authorize(Roles = "Proprietaire,UserIndep,AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetByProprietaire()
        {
            var userId = CurrentUserId();

            var reservations = await _context.Reservations
                .Include(r => r.Bien)
                .Where(r => r.Bien != null && r.Bien.ProprietaireId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReservationDto(r, r.Bien!.Titre))
                .ToListAsync();

            return Ok(new { success = true, data = reservations });
        }

        // ─── PUT /api/reservations/{id}/statut ────────────────────────────────
        // Le propriétaire de l'annonce accepte ou refuse une demande de visite
        [HttpPut("{id:int}/statut")]
        [Authorize]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] UpdateStatutRequest req)
        {
            if (req.Statut != "Confirmée" && req.Statut != "Refusée")
                return BadRequest(new { success = false, message = "Statut invalide. Valeurs acceptées : \"Confirmée\", \"Refusée\"." });

            var reservation = await _context.Reservations
                .Include(r => r.Bien)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound(new { success = false, message = "Réservation introuvable." });

            if ((reservation.Bien == null || reservation.Bien.ProprietaireId != CurrentUserId()) && !IsAdmin())
                return Forbid();

            reservation.Statut = req.Statut;

            NotificationHelper.Creer(_context, reservation.UserId,
                req.Statut == "Confirmée" ? "ReservationConfirmee" : "ReservationRefusee",
                $"Réservation {req.Statut.ToLower()}",
                $"Votre demande de visite pour « {reservation.Bien!.Titre} » a été {req.Statut.ToLower()}.",
                "reservations");

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Réservation {req.Statut.ToLower()}.", data = new ReservationDto(reservation, reservation.Bien!.Titre) });
        }

        // ─── GET /api/reservations/{id} ───────────────────────────────────────
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            var r = await _context.Reservations
                .Include(r => r.Bien)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (r == null)
                return NotFound(new { success = false, message = "Réservation introuvable." });

            if (r.UserId != CurrentUserId() && !IsAdmin())
                return Forbid();

            return Ok(new { success = true, data = new ReservationDto(r, r.Bien!.Titre) });
        }

        // ─── DELETE /api/reservations/{id} ────────────────────────────────────
        // Annuler une réservation
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
                return NotFound(new { success = false, message = "Réservation introuvable." });

            if (reservation.UserId != CurrentUserId() && !IsAdmin())
                return Forbid();

            reservation.Statut = "Annulée";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Réservation annulée." });
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────
    public class ReservationDto
    {
        public int      Id          { get; set; }
        public int      UserId      { get; set; }
        public int      BienId      { get; set; }
        public string   BienTitre   { get; set; }
        public string   Prenom      { get; set; }
        public string   Nom         { get; set; }
        public string   Email       { get; set; }
        public string   Telephone   { get; set; }
        public string   Lieu        { get; set; }
        public DateTime DateVisite  { get; set; }
        public string   Message     { get; set; }
        public string   Statut      { get; set; }
        public DateTime CreatedAt   { get; set; }

        public ReservationDto(Reservation r, string bienTitre)
        {
            Id         = r.Id;
            UserId     = r.UserId;
            BienId     = r.BienId;
            BienTitre  = bienTitre;
            Prenom     = r.Prenom;
            Nom        = r.Nom;
            Email      = r.Email;
            Telephone  = r.Telephone;
            Lieu       = r.Lieu;
            DateVisite = r.DateVisite;
            Message    = r.Message;
            Statut     = r.Statut;
            CreatedAt  = r.CreatedAt;
        }
    }

    // ─── REQUEST MODEL ────────────────────────────────────────────────────────
    public class CreateReservationRequest
    {
        public int      UserId     { get; set; }
        public int      BienId     { get; set; }
        public string   Prenom     { get; set; } = string.Empty;
        public string   Nom        { get; set; } = string.Empty;
        public string   Email      { get; set; } = string.Empty;
        public string   Telephone  { get; set; } = string.Empty;
        public string   Lieu       { get; set; } = string.Empty;
        public DateTime DateVisite { get; set; }
        public string?  Message    { get; set; }
    }

    public class UpdateStatutRequest
    {
        public string Statut { get; set; } = string.Empty;
    }
}