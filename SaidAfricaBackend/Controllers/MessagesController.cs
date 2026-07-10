using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend.Services;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;
        public MessagesController(ApplicationDbContext context, IEmailService email)
        {
            _context = context;
            _email   = email;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ─── GET /api/messages/conversations ─────────────────────────────────────
        // Liste des conversations de l'utilisateur courant (un par interlocuteur+bien)
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var me = CurrentUserId();

            var messages = await _context.Messages
                .Include(m => m.Expediteur)
                .Include(m => m.Destinataire)
                .Include(m => m.Bien)
                .Where(m => m.ExpediteurId == me || m.DestinataireId == me)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            // Grouper par (autreUserId, bienId)
            var conversations = messages
                .GroupBy(m => new
                {
                    AutreUserId = m.ExpediteurId == me ? m.DestinataireId : m.ExpediteurId,
                    BienId = m.BienId
                })
                .Select(g =>
                {
                    var last = g.First();
                    var autreUser = last.ExpediteurId == me ? last.Destinataire : last.Expediteur;
                    return new ConversationDto
                    {
                        AutreUserId    = g.Key.AutreUserId,
                        AutreUserNom   = $"{autreUser?.Prenom} {autreUser?.Nom}".Trim(),
                        BienId         = g.Key.BienId,
                        BienTitre      = last.Bien?.Titre,
                        DernierMessage = last.Contenu,
                        DernierMessageAt = last.CreatedAt,
                        NonLus         = g.Count(m => !m.EstLu && m.DestinataireId == me)
                    };
                })
                .OrderByDescending(c => c.DernierMessageAt)
                .ToList();

            return Ok(new { success = true, data = conversations });
        }

        // ─── GET /api/messages/{autreUserId}?bienId=X ────────────────────────────
        // Messages entre l'utilisateur courant et autreUserId (optionnellement filtrés par bien)
        [HttpGet("{autreUserId:int}")]
        public async Task<IActionResult> GetThread(int autreUserId, [FromQuery] int? bienId)
        {
            var me = CurrentUserId();

            var query = _context.Messages
                .Include(m => m.Expediteur)
                .Where(m =>
                    (m.ExpediteurId == me && m.DestinataireId == autreUserId) ||
                    (m.ExpediteurId == autreUserId && m.DestinataireId == me));

            if (bienId.HasValue)
                query = query.Where(m => m.BienId == bienId);

            var msgs = await query.OrderBy(m => m.CreatedAt).ToListAsync();

            // Marquer comme lus les messages reçus
            var nonLus = msgs.Where(m => m.DestinataireId == me && !m.EstLu).ToList();
            nonLus.ForEach(m => m.EstLu = true);
            if (nonLus.Any()) await _context.SaveChangesAsync();

            return Ok(new { success = true, data = msgs.Select(m => new MessageDto(m, me)) });
        }

        // ─── POST /api/messages ───────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Send([FromBody] SendMessageRequest req)
        {
            var me = CurrentUserId();

            if (string.IsNullOrWhiteSpace(req.Contenu))
                return BadRequest(new { success = false, message = "Le message ne peut pas être vide." });

            var destinataire = await _context.Users.FindAsync(req.DestinataireId);
            if (destinataire == null)
                return NotFound(new { success = false, message = "Destinataire introuvable." });

            var msg = new Message
            {
                ExpediteurId   = me,
                DestinataireId = req.DestinataireId,
                BienId         = req.BienId,
                Contenu        = req.Contenu.Trim(),
            };

            _context.Messages.Add(msg);

            // Notification au destinataire
            var moi = await _context.Users.FindAsync(me);
            var bienTitre = req.BienId.HasValue
                ? (await _context.Biens.FindAsync(req.BienId))?.Titre
                : null;
            var titreNotif = bienTitre != null ? $"Nouveau message · {bienTitre}" : "Nouveau message";
            NotificationHelper.Creer(_context, req.DestinataireId,
                "NouveauMessage", titreNotif,
                $"{moi?.Prenom} {moi?.Nom} vous a envoyé un message.",
                "messagerie");

            await _context.SaveChangesAsync();

            // Email au destinataire (fire-and-forget — ne bloque pas la réponse)
            _ = _email.SendNouveauMessageAsync(
                destinataire.Email,
                destinataire.Prenom,
                moi?.Prenom ?? "Quelqu'un",
                bienTitre ?? "Said Africa",
                req.Contenu.Trim().Length > 120 ? req.Contenu.Trim()[..120] : req.Contenu.Trim());

            // Recharger avec navigation
            await _context.Entry(msg).Reference(m => m.Expediteur).LoadAsync();
            return Ok(new { success = true, data = new MessageDto(msg, me) });
        }

        // ─── DELETE /api/messages/{id} ────────────────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var msg = await _context.Messages.FindAsync(id);
            if (msg == null) return NotFound(new { success = false, message = "Message introuvable." });
            if (msg.ExpediteurId != CurrentUserId())
                return Forbid();

            _context.Messages.Remove(msg);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    // ─── DTOs ──────────────────────────────────────────────────────────────────
    public class MessageDto
    {
        public int      Id             { get; set; }
        public int      ExpediteurId   { get; set; }
        public int      DestinataireId { get; set; }
        public int?     BienId         { get; set; }
        public string   Contenu        { get; set; }
        public bool     EstLu          { get; set; }
        public bool     EstMoi         { get; set; }
        public string   ExpediteurNom  { get; set; }
        public DateTime CreatedAt      { get; set; }

        public MessageDto(Message m, int meId)
        {
            Id             = m.Id;
            ExpediteurId   = m.ExpediteurId;
            DestinataireId = m.DestinataireId;
            BienId         = m.BienId;
            Contenu        = m.Contenu;
            EstLu          = m.EstLu;
            EstMoi         = m.ExpediteurId == meId;
            ExpediteurNom  = $"{m.Expediteur?.Prenom} {m.Expediteur?.Nom}".Trim();
            CreatedAt      = m.CreatedAt;
        }
    }

    public class ConversationDto
    {
        public int      AutreUserId      { get; set; }
        public string   AutreUserNom     { get; set; } = string.Empty;
        public int?     BienId           { get; set; }
        public string?  BienTitre        { get; set; }
        public string   DernierMessage   { get; set; } = string.Empty;
        public DateTime DernierMessageAt { get; set; }
        public int      NonLus           { get; set; }
    }

    public class SendMessageRequest
    {
        public int    DestinataireId { get; set; }
        public int?   BienId         { get; set; }
        public string Contenu        { get; set; } = string.Empty;
    }
}
