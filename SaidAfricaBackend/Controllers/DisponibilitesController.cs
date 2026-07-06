using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DisponibilitesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public DisponibilitesController(ApplicationDbContext context) => _context = context;

        private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // ─── GET /api/disponibilites/{bienId} ────────────────────────────────────
        // Retourne toutes les dates bloquées pour un bien (les 6 prochains mois)
        [HttpGet("{bienId:int}")]
        public async Task<IActionResult> GetByBien(int bienId)
        {
            var bien = await _context.Biens.FindAsync(bienId);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            var from = DateTime.UtcNow.Date;
            var to   = from.AddMonths(6);

            var dates = await _context.Disponibilites
                .Where(d => d.BienId == bienId && d.Date >= from && d.Date <= to)
                .OrderBy(d => d.Date)
                .Select(d => new DisponibiliteDto(d))
                .ToListAsync();

            return Ok(new { success = true, data = dates });
        }

        // ─── POST /api/disponibilites ─────────────────────────────────────────────
        // Bloquer une date (idempotent : si elle existe déjà, on la supprime = toggle)
        [HttpPost]
        public async Task<IActionResult> Toggle([FromBody] ToggleDispoRequest req)
        {
            var bien = await _context.Biens.FindAsync(req.BienId);
            if (bien == null)
                return NotFound(new { success = false, message = "Bien introuvable." });

            if (bien.ProprietaireId != CurrentUserId())
                return Forbid();

            var date = req.Date.Date; // normalise à minuit UTC
            var existing = await _context.Disponibilites
                .FirstOrDefaultAsync(d => d.BienId == req.BienId && d.Date == date);

            if (existing != null)
            {
                _context.Disponibilites.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, action = "removed", date });
            }

            var dispo = new Disponibilite { BienId = req.BienId, Date = date, Motif = req.Motif };
            _context.Disponibilites.Add(dispo);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, action = "added", data = new DisponibiliteDto(dispo) });
        }

        // ─── DELETE /api/disponibilites/{id} ─────────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var dispo = await _context.Disponibilites.Include(d => d.Bien).FirstOrDefaultAsync(d => d.Id == id);
            if (dispo == null) return NotFound(new { success = false, message = "Disponibilité introuvable." });

            if (dispo.Bien?.ProprietaireId != CurrentUserId())
                return Forbid();

            _context.Disponibilites.Remove(dispo);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    public class DisponibiliteDto
    {
        public int      Id      { get; set; }
        public int      BienId  { get; set; }
        public DateTime Date    { get; set; }
        public string?  Motif   { get; set; }

        public DisponibiliteDto(Disponibilite d)
        {
            Id     = d.Id;
            BienId = d.BienId;
            Date   = d.Date;
            Motif  = d.Motif;
        }
    }

    public class ToggleDispoRequest
    {
        public int      BienId { get; set; }
        public DateTime Date   { get; set; }
        public string?  Motif  { get; set; }
    }
}
