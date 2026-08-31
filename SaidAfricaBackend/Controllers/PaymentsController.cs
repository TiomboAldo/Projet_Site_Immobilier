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
        private readonly ApplicationDbContext  _context;
        private readonly ICamPayService        _campay;
        private readonly IEmailService         _email;
        private readonly ITaxCommissionService _taxSvc;

        private int    CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole()   => User.FindFirstValue(ClaimTypes.Role) ?? "";

        public PaymentsController(ApplicationDbContext context, ICamPayService campay, IEmailService email, ITaxCommissionService taxSvc)
        {
            _context = context;
            _campay  = campay;
            _email   = email;
            _taxSvc  = taxSvc;
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
            {
                // Environnement sans CamPay : simuler succès immédiat
                payment.Statut = "Reussi";
                await _context.SaveChangesAsync();
                if (payment.ReservationId.HasValue)
                    _ = ConfirmerReservationAsync(payment);
                return Ok(new { success = true, paymentId = payment.Id, unconfigured = true });
            }

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

        // ─── GET /api/payments/calculer/{bienId} ─────────────────────────────
        [HttpGet("calculer/{bienId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Calculer(int bienId)
        {
            var bien = await _context.Biens
                .Include(b => b.Proprietaire)
                .FirstOrDefaultAsync(b => b.Id == bienId);

            if (bien == null) return NotFound(new { success = false });

            var prixStr = System.Text.RegularExpressions.Regex.Replace(bien.Prix ?? "0", @"[^\d]", "");
            decimal.TryParse(prixStr, out var montantBrut);

            var typeTransaction    = (bien.Statut ?? "vente").ToLower();
            var typeCompte         = bien.Proprietaire?.TypeCompteProf ?? "";
            var agenceVendTerrain  = typeCompte == "AgenceImmobiliere" && (bien.Type ?? "").ToLower().Contains("terrain");

            var calc = _taxSvc.Calculer(montantBrut, typeTransaction, typeCompte, agenceVendTerrain);

            return Ok(new { success = true, calcul = calc, titre = bien.Titre, statut = typeTransaction, montantBrut });
        }

        // ─── POST /api/payments/acheter ───────────────────────────────────────
        [HttpPost("acheter")]
        public async Task<IActionResult> Acheter([FromBody] AcheterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NumeroPayeur))
                return BadRequest(new { success = false, message = "Numéro de téléphone requis." });

            var userId = CurrentUserId();

            var bien = await _context.Biens
                .Include(b => b.Proprietaire)
                .FirstOrDefaultAsync(b => b.Id == req.BienId && b.EstDisponible && b.StatutPublication == "Valide");

            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable ou indisponible." });
            if (bien.ProprietaireId == userId)
                return BadRequest(new { success = false, message = "Vous ne pouvez pas acheter votre propre bien." });

            var prixStr = System.Text.RegularExpressions.Regex.Replace(bien.Prix ?? "0", @"[^\d]", "");
            decimal.TryParse(prixStr, out var montantBrut);

            var typeTransaction   = (bien.Statut ?? "vente").ToLower();
            var typeCompte        = bien.Proprietaire?.TypeCompteProf ?? "";
            var agenceVendTerrain = typeCompte == "AgenceImmobiliere" && (bien.Type ?? "").ToLower().Contains("terrain");

            var calc = _taxSvc.Calculer(montantBrut, typeTransaction, typeCompte, agenceVendTerrain);

            if (!calc.GereParLevetimmo)
                return BadRequest(new { success = false, message = "Ce bien se traite en contact direct avec le vendeur." });

            var commTx = new CommissionTransaction
            {
                BienId                = req.BienId,
                AgentId               = bien.ProprietaireId ?? 0,
                TypeTransaction       = typeTransaction,
                MontantBrut           = calc.MontantBrut,
                TauxTaxePct           = calc.TauxTaxePct,
                MontantTaxe           = calc.MontantTaxe,
                MontantNetApresImpots = calc.MontantNetApresImpots,
                CommissionLevetimmo   = calc.CommissionLevetimmo,
                CommissionAgent       = calc.CommissionAgent,
                GereParLevetimmo      = true,
                Statut                = "En cours",
                Notes                 = $"Acheteur:{userId} | Tel:{req.NumeroPayeur}",
            };
            _context.CommissionTransactions.Add(commTx);
            await _context.SaveChangesAsync();

            var payment = new Payment
            {
                TypePaiement  = "Achat",
                Montant       = calc.MontantBrut,
                NumeroPayeur  = req.NumeroPayeur.Trim(),
                UserId        = userId,
                BienId        = req.BienId,
                ReservationId = commTx.Id,
                Statut        = "EnAttente",
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (!_campay.IsConfigured)
            {
                payment.Statut = "Reussi";
                await _context.SaveChangesAsync();
                _ = ConfirmerAchatAsync(payment);
                return Ok(new { success = true, paymentId = payment.Id, unconfigured = true,
                    reference = $"TXN-LVT-{payment.Id:D5}", calcul = calc });
            }

            var label = typeTransaction == "location" ? "Location via Levetimmo" : "Achat via Levetimmo";
            var (ok, reference, error) = await _campay.CollectAsync(
                req.NumeroPayeur.Trim(), calc.MontantBrut, label, payment.Id.ToString());

            if (!ok)
            {
                payment.Statut     = "Echoue";
                payment.MotifEchec = error;
                commTx.Statut      = "Annulée";
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = error ?? "Erreur lors du paiement." });
            }

            payment.ReferenceId = reference;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, paymentId = payment.Id, reference, calcul = calc });
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
                var wasEnAttente = payment.Statut == "EnAttente";
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

                if (wasEnAttente && payment.Statut == "Reussi")
                {
                    if (payment.TypePaiement == "Achat")
                        _ = ConfirmerAchatAsync(payment);
                    else if (payment.ReservationId.HasValue)
                        _ = ConfirmerReservationAsync(payment);
                }
            }

            return Ok(new { success = true, statut = payment.Statut });
        }

        // ─── POST /api/payments/callback ──────────────────────────────────────
        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromBody] CamPayWebhookPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.ExternalReference)) return Ok();
            if (!int.TryParse(payload.ExternalReference, out var paymentId)) return Ok();

            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null) return Ok();

            var wasEnAttente   = payment.Statut == "EnAttente";
            payment.Statut     = payload.Status == "SUCCESSFUL" ? "Reussi" : "Echoue";
            payment.UpdatedAt  = DateTime.UtcNow;
            if (payload.Status != "SUCCESSFUL") payment.MotifEchec = "Paiement refusé ou annulé";

            await _context.SaveChangesAsync();

            if (wasEnAttente && payment.Statut == "Reussi")
            {
                if (payment.TypePaiement == "Achat")
                    _ = ConfirmerAchatAsync(payment);
                else if (payment.ReservationId.HasValue)
                    _ = ConfirmerReservationAsync(payment);
            }

            return Ok();
        }

        private async Task ConfirmerAchatAsync(Payment payment)
        {
            try
            {
                if (!payment.ReservationId.HasValue) return;
                var commTx = await _context.CommissionTransactions.FindAsync(payment.ReservationId.Value);
                if (commTx == null) return;

                commTx.Statut = "Complète";
                await _context.SaveChangesAsync();

                var bien = await _context.Biens.Include(b => b.Proprietaire)
                    .FirstOrDefaultAsync(b => b.Id == payment.BienId);
                if (bien?.Proprietaire == null) return;

                NotificationHelper.Creer(_context, bien.ProprietaireId,
                    "TransactionConfirmee", "Transaction confirmée via Levetimmo",
                    $"Paiement reçu pour « {bien.Titre} ». Votre part : {commTx.CommissionAgent:N0} XAF.",
                    "transactions");

                var admins = await _context.Users
                    .Where(u => u.Role == "AdminRegion" || u.Role == "AdminPays" || u.Role == "DirecteurProjet")
                    .ToListAsync();

                foreach (var admin in admins)
                    NotificationHelper.Creer(_context, admin.Id,
                        "NouvelleCommission", "Nouvelle commission Levetimmo",
                        $"Transaction « {bien.Titre} » : {commTx.CommissionLevetimmo:N0} XAF de commission.",
                        "transactions");

                await _context.SaveChangesAsync();
            }
            catch { /* non bloquant */ }
        }

        private async Task ConfirmerReservationAsync(Payment payment)
        {
            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.Bien)
                        .ThenInclude(b => b!.Proprietaire)
                    .FirstOrDefaultAsync(r => r.Id == payment.ReservationId);

                if (reservation == null) return;

                // Débloquer la réservation maintenant que le paiement est confirmé
                if (reservation.Statut == "En attente de paiement")
                {
                    reservation.Statut = "En attente";
                    await _context.SaveChangesAsync();
                }

                if (reservation.Bien?.Proprietaire == null) return;

                var proprio = reservation.Bien.Proprietaire;
                var dateStr = reservation.DateVisite.ToString("dd/MM/yyyy");

                // Notification in-app pour le publieur
                NotificationHelper.Creer(_context, proprio.Id,
                    "NouvelleReservation", "Nouvelle réservation reçue",
                    $"{reservation.Prenom} {reservation.Nom} a réservé une visite pour « {reservation.Bien.Titre} » (paiement confirmé).",
                    "reservations");
                await _context.SaveChangesAsync();

                // Email au publieur
                await _email.SendNouvelleReservationAsync(
                    proprio.Email, proprio.Prenom,
                    reservation.Bien.Titre,
                    reservation.Prenom, reservation.Nom, dateStr);
            }
            catch { /* non bloquant */ }
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

        // ─── GET /api/payments/admin (admin only) ───────────────────────────
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

    public class AcheterRequest
    {
        public int    BienId       { get; set; }
        public string NumeroPayeur { get; set; } = string.Empty;
        public string Operateur    { get; set; } = "mtn";
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
