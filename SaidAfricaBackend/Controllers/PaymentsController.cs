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
        private readonly IMoMoService _momo;

        private int    CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole()   => User.FindFirstValue(ClaimTypes.Role) ?? "";

        public PaymentsController(ApplicationDbContext context, IMoMoService momo)
        {
            _context = context;
            _momo    = momo;
        }

        // ─── POST /api/payments/initier ───────────────────────────────────────
        // Corps : { TypePaiement, Montant, NumeroPayeur, BienId?, ReservationId? }
        [HttpPost("initier")]
        public async Task<IActionResult> Initier([FromBody] InitierPaiementRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NumeroPayeur))
                return BadRequest(new { success = false, message = "Numéro MTN requis." });

            if (req.Montant <= 0)
                return BadRequest(new { success = false, message = "Montant invalide." });

            var validTypes = new[] { "Reservation", "Abonnement", "Commission" };
            if (!validTypes.Contains(req.TypePaiement))
                return BadRequest(new { success = false, message = "Type de paiement invalide." });

            var userId = CurrentUserId();

            // Créer l'enregistrement Payment en base (statut EnAttente)
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

            // Appel MTN MoMo
            if (_momo.IsConfigured)
            {
                var description = req.TypePaiement switch
                {
                    "Reservation"  => $"Frais de visite — Said Africa",
                    "Abonnement"   => $"Abonnement Propriétaire — Said Africa",
                    "Commission"   => $"Commission — Said Africa",
                    _              => "Said Africa",
                };

                var (success, referenceId, error) = await _momo.InitiateCollectionAsync(
                    phoneNumber: req.NumeroPayeur.Trim(),
                    amount:      req.Montant,
                    description: description,
                    externalId:  payment.Id.ToString());

                if (!success)
                {
                    payment.Statut     = "Echoue";
                    payment.MotifEchec = error;
                    await _context.SaveChangesAsync();
                    return BadRequest(new { success = false, message = error ?? "Erreur lors de l'envoi de la demande de paiement." });
                }

                // Stocker le referenceId MTN dans le champ ReferenceId
                payment.ReferenceId = referenceId;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, paymentId = payment.Id, referenceId });
            }
            else
            {
                // Mode sans credentials : retourner le paymentId pour simulation côté frontend
                return Ok(new { success = true, paymentId = payment.Id, referenceId = payment.ReferenceId, unconfigured = true });
            }
        }

        // ─── GET /api/payments/status/{paymentId} ─────────────────────────────
        [HttpGet("status/{paymentId:int}")]
        public async Task<IActionResult> GetStatus(int paymentId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == CurrentUserId());

            if (payment == null)
                return NotFound(new { success = false, message = "Paiement introuvable." });

            // Si déjà résolu, pas besoin de rappeler MTN
            if (payment.Statut != "EnAttente")
                return Ok(new { success = true, statut = payment.Statut });

            if (_momo.IsConfigured)
            {
                var momoStatus = await _momo.GetPaymentStatusAsync(payment.ReferenceId);

                payment.Statut = momoStatus switch
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
        // Webhook MTN MoMo (notification automatique après paiement)
        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromBody] MoMoCallbackPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.ExternalId)) return Ok();

            if (!int.TryParse(payload.ExternalId, out var paymentId)) return Ok();

            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null) return Ok();

            payment.Statut    = payload.Status == "SUCCESSFUL" ? "Reussi" : "Echoue";
            payment.UpdatedAt = DateTime.UtcNow;
            if (payload.Status != "SUCCESSFUL")
                payment.MotifEchec = payload.Reason ?? "Paiement refusé";

            await _context.SaveChangesAsync();
            return Ok();
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

    public class MoMoCallbackPayload
    {
        public string? ExternalId { get; set; }
        public string? Status     { get; set; }
        public string? Reason     { get; set; }
    }
}
