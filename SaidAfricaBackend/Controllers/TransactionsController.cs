using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend.Services;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly ApplicationDbContext  _context;
        private readonly ITaxCommissionService _taxSvc;

        public TransactionsController(ApplicationDbContext context, ITaxCommissionService taxSvc)
        {
            _context = context;
            _taxSvc  = taxSvc;
        }

        // ── GET /api/transactions/calculer-taxe ───────────────────────────────
        // Calculateur public : renvoie la décomposition taxe + commission
        [HttpGet("calculer-taxe")]
        public IActionResult CalculerTaxe(
            [FromQuery] decimal montant,
            [FromQuery] string  typeTransaction,    // "vente" | "location"
            [FromQuery] string  typeCompteProf = "Proprietaire",
            [FromQuery] bool    agenceVendTerrain = false)
        {
            if (montant <= 0)
                return BadRequest(new { success = false, message = "Le montant doit être positif." });
            if (string.IsNullOrWhiteSpace(typeTransaction))
                return BadRequest(new { success = false, message = "typeTransaction requis (vente ou location)." });

            var result = _taxSvc.Calculer(montant, typeTransaction, typeCompteProf, agenceVendTerrain);
            return Ok(new { success = true, data = result });
        }

        // ── GET /api/transactions/abonnement-statut/{userId} ──────────────────
        // Statut abonnement d'un agent (admin + agent lui-même)
        [HttpGet("abonnement-statut/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetAbonnementStatut(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false });

            // Seul l'agent lui-même ou un admin peut consulter
            var callerId   = int.Parse(User.FindFirst("sub")?.Value ?? "0");
            var callerRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            if (callerId != userId && callerRole is not ("AdminRegion" or "AdminPays" or "DirecteurProjet"))
                return Forbid();

            if (user.TypeCompteProf != "AgentImmobilier")
                return Ok(new { success = true, data = new { message = "Pas un agent immobilier — aucun abonnement requis." } });

            var statut = _taxSvc.GetStatutAbonnement(user);
            return Ok(new { success = true, data = statut });
        }

        // ── POST /api/transactions ─────────────────────────────────────────────
        // Enregistrer une transaction avec commission (agent ou admin)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreerTransaction([FromBody] CreerTransactionRequest req)
        {
            if (req.MontantBrut <= 0 || string.IsNullOrWhiteSpace(req.TypeTransaction))
                return BadRequest(new { success = false, message = "Montant et type de transaction requis." });

            var agent = await _context.Users.FindAsync(req.AgentId);
            if (agent == null)
                return NotFound(new { success = false, message = "Agent introuvable." });

            // Vérifier abonnement si AgentImmobilier
            if (agent.TypeCompteProf == "AgentImmobilier")
            {
                var statut = _taxSvc.GetStatutAbonnement(agent);
                if (statut.DoitPayer)
                    return BadRequest(new { success = false, message = "L'agent doit renouveler son abonnement annuel avant d'enregistrer une transaction.", doitPayer = true });
            }

            var calc = _taxSvc.Calculer(req.MontantBrut, req.TypeTransaction, agent.TypeCompteProf ?? "", req.AgenceVendTerrain);

            var tx = new CommissionTransaction
            {
                BienId               = req.BienId,
                AgentId              = req.AgentId,
                TypeTransaction      = req.TypeTransaction,
                MontantBrut          = calc.MontantBrut,
                TauxTaxePct          = calc.TauxTaxePct,
                MontantTaxe          = calc.MontantTaxe,
                MontantNetApresImpots = calc.MontantNetApresImpots,
                CommissionLevetimmo  = calc.CommissionLevetimmo,
                CommissionAgent      = calc.CommissionAgent,
                GereParLevetimmo     = calc.GereParLevetimmo,
                Statut               = "En cours",
                Notes                = req.Notes,
                CreatedAt            = DateTime.UtcNow,
            };

            _context.CommissionTransactions.Add(tx);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Transaction enregistrée.", data = new
            {
                tx.Id,
                tx.MontantBrut,
                tx.TauxTaxePct,
                tx.MontantTaxe,
                tx.MontantNetApresImpots,
                tx.CommissionLevetimmo,
                tx.CommissionAgent,
                tx.GereParLevetimmo,
                tx.Statut,
                Explication = calc.Explication,
            }});
        }

        // ── GET /api/transactions ─────────────────────────────────────────────
        // Liste toutes les transactions (admin seulement)
        [HttpGet]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _context.CommissionTransactions
                .Include(t => t.Agent)
                .Include(t => t.Bien)
                .OrderByDescending(t => t.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.TypeTransaction,
                    t.MontantBrut,
                    t.TauxTaxePct,
                    t.MontantTaxe,
                    t.MontantNetApresImpots,
                    t.CommissionLevetimmo,
                    t.CommissionAgent,
                    t.GereParLevetimmo,
                    t.Statut,
                    t.Notes,
                    t.CreatedAt,
                    Agent = t.Agent == null ? null : new { t.Agent.Id, t.Agent.Prenom, t.Agent.Nom, t.Agent.Email, t.Agent.TypeCompteProf },
                    Bien  = t.Bien  == null ? null : new { t.Bien.Id,  t.Bien.Titre,  t.Bien.Type, t.Bien.Localisation },
                })
                .ToListAsync();

            return Ok(new { success = true, total, page, pageSize, data = items });
        }

        // ── PATCH /api/transactions/{id}/statut ───────────────────────────────
        // Mettre à jour le statut d'une transaction (admin)
        [HttpPatch("{id}/statut")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] UpdateStatutRequest req)
        {
            var tx = await _context.CommissionTransactions.FindAsync(id);
            if (tx == null) return NotFound(new { success = false });

            tx.Statut = req.Statut;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ── POST /api/transactions/abonnement-payer/{userId} ──────────────────
        // Activer/renouveler l'abonnement annuel d'un agent (admin seulement)
        [HttpPost("abonnement-payer/{userId}")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> PayerAbonnement(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false });

            if (user.TypeCompteProf != "AgentImmobilier")
                return BadRequest(new { success = false, message = "Cet utilisateur n'est pas un agent immobilier." });

            // Prolonger depuis la date d'expiration existante (renouvellement) ou depuis aujourd'hui
            var base_ = (user.AbonnementExpireLe.HasValue && user.AbonnementExpireLe.Value > DateTime.UtcNow)
                ? user.AbonnementExpireLe.Value
                : DateTime.UtcNow;

            user.AbonnementExpireLe = base_.AddYears(1);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Abonnement activé jusqu'au {user.AbonnementExpireLe.Value:dd/MM/yyyy}.", expireLe = user.AbonnementExpireLe });
        }
    }

    public class CreerTransactionRequest
    {
        public int?    BienId           { get; set; }
        public int     AgentId          { get; set; }
        public string  TypeTransaction  { get; set; } = string.Empty;  // "vente" | "location"
        public decimal MontantBrut      { get; set; }
        public bool    AgenceVendTerrain { get; set; } = false;
        public string? Notes            { get; set; }
    }

    public class UpdateStatutRequest
    {
        public string Statut { get; set; } = string.Empty;  // "En cours" | "Complète" | "Annulée"
    }
}
