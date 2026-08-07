using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend.Services;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICamPayService _campay;
        private readonly IEmailService _email;

        private int    CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole()   => User.FindFirstValue(ClaimTypes.Role) ?? "";

        public PaymentsController(ApplicationDbContext context, ICamPayService campay, IEmailService email)
        {
            _context = context;
            _campay  = campay;
            _email   = email;
        }

        // ─── POST /api/payments/initier ───────────────────────────────────────
        [HttpPost("initier")]
        public async Task<IActionResult> Initier([FromBody] InitierPaiementRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NumeroPayeur))
                return BadRequest(new { success = false, message = "Numéro de téléphone requis." });

            if (req.Montant <= 0)
                return BadRequest(new { success = false, message = "Montant invalide." });

            var validTypes = new[] { "Reservation", "Abonnement", "Commission" };
            if (!validTypes.Contains(req.TypePaiement))
                return BadRequest(new { success = false, message = "Type de paiement invalide." });

            var userId = CurrentUserId();

            var payment = new Payment
            {
                TypePaiement  = req.TypePaiement,
                Montant       = req.Montant,
                NumeroPayeur  = req.NumeroPayeur.Trim(),
                UserId        = userId,
                BienId        = req.BienId,
                ReservationId = req.ReservationId,
                Statut        = "EnAttente",
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (!_campay.IsConfigured)
                return Ok(new { success = true, paymentId = payment.Id, unconfigured = true });

            var description = req.TypePaiement switch
            {
                "Reservation" => "Frais de visite — Levetimmo",
                "Abonnement"  => "Abonnement Propriétaire — Levetimmo",
                "Commission"  => "Commission — Levetimmo",
                _             => "Levetimmo",
            };

            var (success, reference, error) = await _campay.CollectAsync(
                phoneNumber:       req.NumeroPayeur.Trim(),
                amount:            req.Montant,
                description:       description,
                externalReference: payment.Id.ToString());

            if (!success)
            {
                payment.Statut     = "Echoue";
                payment.MotifEchec = error;
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = error ?? "Erreur lors de l'envoi de la demande de paiement." });
            }

            payment.ReferenceId = reference;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, paymentId = payment.Id, reference });
        }

        // ─── GET /api/payments/status/{paymentId} ─────────────────────────────
        [HttpGet("status/{paymentId:int}")]
        public async Task<IActionResult> GetStatus(int paymentId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == CurrentUserId());

            if (payment == null)
                return NotFound(new { success = false, message = "Paiement introuvable." });

            if (payment.Statut != "EnAttente")
                return Ok(new { success = true, statut = payment.Statut });

            if (_campay.IsConfigured && !string.IsNullOrEmpty(payment.ReferenceId))
            {
                var campayStatus = await _campay.GetStatusAsync(payment.ReferenceId);

                payment.Statut = campayStatus switch
                {
                    "SUCCESSFUL" => "Reussi",
                    "FAILED"     => "Echoue",
                    _            => "EnAttente",
                };

                if (payment.Statut != "EnAttente")
                    payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, statut = payment.Statut });
        }

        // ─── POST /api/payments/callback ──────────────────────────────────────
        // Webhook CamPay (notification automatique après paiement)
        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromBody] CamPayWebhookPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.ExternalReference)) return Ok();
            if (!int.TryParse(payload.ExternalReference, out var paymentId)) return Ok();

            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null) return Ok();

            var wasEnAttente = payment.Statut == "EnAttente";

            payment.Statut    = payload.Status == "SUCCESSFUL" ? "Reussi" : "Echoue";
            payment.UpdatedAt = DateTime.UtcNow;
            if (payload.Status != "SUCCESSFUL")
                payment.MotifEchec = "Paiement refusé ou annulé";

            await _context.SaveChangesAsync();

            // Notifier le propriétaire uniquement lors du premier succès
            if (wasEnAttente && payment.Statut == "Reussi" && payment.ReservationId.HasValue)
                _ = NotifierProprietaireAsync(payment);

            return Ok();
        }

        private async Task NotifierProprietaireAsync(Payment payment)
        {
            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.Bien)
                        .ThenInclude(b => b!.Proprietaire)
                    .FirstOrDefaultAsync(r => r.Id == payment.ReservationId);

                if (reservation?.Bien?.Proprietaire == null) return;

                var proprio = reservation.Bien.Proprietaire;
                var dateStr = reservation.DateVisite.HasValue
                    ? reservation.DateVisite.Value.ToString("dd/MM/yyyy")
                    : "non précisée";

                await _email.SendPaiementConfirmeProprietaireAsync(
                    proprio.Email, proprio.Prenom,
                    reservation.Bien.Titre,
                    reservation.Prenom, reservation.Nom,
                    payment.Montant, dateStr);
            }
            catch { /* email non bloquant */ }
        }

        // ─── GET /api/payments/mes-paiements ─────────────────────────────────
        [HttpGet("mes-paiements")]
        public async Task<IActionResult> MesPaiements()
        {
            var userId = CurrentUserId();
            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.TypePaiement, p.Montant, p.Devise,
                    p.Statut, p.NumeroPayeur, p.CreatedAt, p.UpdatedAt,
                    p.BienId, p.ReservationId,
                })
                .ToListAsync();

            return Ok(new { success = true, data = payments });
        }

        // ─── GET /api/payments/admin (admin only) ─────────────────────────────
        [HttpGet("admin")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> AdminPaiements()
        {
            var payments = await _context.Payments
                .Include(p => p.User)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.TypePaiement, p.Montant, p.Devise, p.Statut,
                    p.NumeroPayeur, p.CreatedAt,
                    User = new { p.User!.Prenom, p.User.Nom, p.User.Email },
                    p.BienId, p.ReservationId,
                })
                .Take(200)
                .ToListAsync();

            return Ok(new { success = true, data = payments });
        }
    }

    public class InitierPaiementRequest
    {
        public string  TypePaiement  { get; set; } = string.Empty;
        public decimal Montant       { get; set; }
        public string  NumeroPayeur  { get; set; } = string.Empty;
        public int?    BienId        { get; set; }
        public int?    ReservationId { get; set; }
    }

    public class CamPayWebhookPayload
    {
        public string? Reference         { get; set; }
        public string? Status            { get; set; }
        public string? ExternalReference { get; set; }
        public string? Operator          { get; set; }
        public string? Amount            { get; set; }
    }
}
