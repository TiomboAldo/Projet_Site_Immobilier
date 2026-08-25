using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend.Services;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsletterController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _email;

        public NewsletterController(ApplicationDbContext context, IEmailService email)
        {
            _context = context;
            _email   = email;
        }

        // POST /api/newsletter/subscribe
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] NewsletterSubscribeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
                return BadRequest(new { success = false, message = "Email invalide." });

            var email = req.Email.Trim().ToLower();

            var exists = await _context.NewsletterSubscribers.AnyAsync(s => s.Email == email);
            if (exists)
                return Ok(new { success = true, message = "Vous êtes déjà abonné à notre newsletter." });

            _context.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = email });
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Merci ! Vous êtes maintenant abonné à notre newsletter." });
        }

        // GET /api/newsletter/subscribers (admin)
        [HttpGet("subscribers")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> GetSubscribers()
        {
            var list = await _context.NewsletterSubscribers
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new { s.Id, s.Email, s.CreatedAt })
                .ToListAsync();

            return Ok(new { success = true, count = list.Count, subscribers = list });
        }

        // DELETE /api/newsletter/subscribers/{id} (admin)
        [HttpDelete("subscribers/{id}")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> DeleteSubscriber(int id)
        {
            var sub = await _context.NewsletterSubscribers.FindAsync(id);
            if (sub == null) return NotFound(new { success = false });
            _context.NewsletterSubscribers.Remove(sub);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // POST /api/newsletter/send (admin)
        [HttpPost("send")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> SendNewsletter([FromBody] SendNewsletterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Sujet) || string.IsNullOrWhiteSpace(req.Corps))
                return BadRequest(new { success = false, message = "Le sujet et le contenu sont requis." });

            var subscribers = await _context.NewsletterSubscribers.Select(s => s.Email).ToListAsync();
            if (subscribers.Count == 0)
                return Ok(new { success = true, message = "Aucun abonné.", envoyes = 0 });

            int envoyes = 0;
            foreach (var emailAddr in subscribers)
            {
                try
                {
                    await _email.SendAsync(emailAddr, emailAddr, req.Sujet, req.Corps);
                    envoyes++;
                }
                catch { /* un échec n'arrête pas les autres */ }
            }

            return Ok(new { success = true, message = $"Newsletter envoyée à {envoyes}/{subscribers.Count} abonné(s).", envoyes });
        }
    }

    public class NewsletterSubscribeRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class SendNewsletterRequest
    {
        public string Sujet { get; set; } = string.Empty;
        public string Corps { get; set; } = string.Empty;
    }
}
