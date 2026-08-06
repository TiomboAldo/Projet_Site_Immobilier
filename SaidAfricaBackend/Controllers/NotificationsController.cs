using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ─── GET /api/notifications ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = CurrentUserId();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new {
                    n.Id,
                    n.Type,
                    n.Titre,
                    n.Message,
                    n.CibleSection,
                    n.EstLue,
                    n.CreatedAt
                })
                .ToListAsync();

            var nonLues = notifications.Count(n => !n.EstLue);

            return Ok(new { success = true, data = new { nonLues, notifications } });
        }

        // ─── PUT /api/notifications/{id}/lue ─────────────────────────────────────
        [HttpPut("{id:int}/lue")]
        public async Task<IActionResult> MarquerLue(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound(new { success = false, message = "Notification introuvable." });

            if (notification.UserId != CurrentUserId())
                return Forbid();

            notification.EstLue = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // ─── PUT /api/notifications/lire-tout ────────────────────────────────────
        [HttpPut("lire-tout")]
        public async Task<IActionResult> MarquerToutesLues()
        {
            var userId = CurrentUserId();
            var nonLues = await _context.Notifications
                .Where(n => n.UserId == userId && !n.EstLue)
                .ToListAsync();

            foreach (var n in nonLues) n.EstLue = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // ─── DELETE /api/notifications/{id} ─────────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound(new { success = false, message = "Notification introuvable." });

            if (notification.UserId != CurrentUserId())
                return Forbid();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
