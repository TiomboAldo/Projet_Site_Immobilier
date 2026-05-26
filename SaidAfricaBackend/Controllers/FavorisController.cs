using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FavorisController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FavorisController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─── GET /api/favoris/user/{userId} ───────────────────────────────────
        // Récupérer tous les favoris d'un utilisateur (avec les détails du bien)
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var favoris = await _context.Favoris
                .Where(f => f.UserId == userId)
                .Include(f => f.Bien)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FavoriDto
                {
                    Id        = f.Id,
                    UserId    = f.UserId,
                    BienId    = f.BienId,
                    CreatedAt = f.CreatedAt,
                    Bien      = f.Bien != null ? new BienDto(f.Bien) : null
                })
                .ToListAsync();

            return Ok(new { success = true, data = favoris });
        }

        // ─── POST /api/favoris ────────────────────────────────────────────────
        // Ajouter un bien aux favoris
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddFavoriRequest req)
        {
            // Vérifier qu'il n'est pas déjà en favori
            var exists = await _context.Favoris
                .AnyAsync(f => f.UserId == req.UserId && f.BienId == req.BienId);

            if (exists)
                return BadRequest(new { success = false, message = "Ce bien est déjà dans vos favoris." });

            var favori = new Favori
            {
                UserId = req.UserId,
                BienId = req.BienId
            };

            _context.Favoris.Add(favori);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Ajouté aux favoris.", data = new { favori.Id, favori.UserId, favori.BienId } });
        }

        // ─── DELETE /api/favoris/{userId}/{bienId} ────────────────────────────
        // Retirer un bien des favoris
        [HttpDelete("{userId}/{bienId}")]
        public async Task<IActionResult> Remove(int userId, int bienId)
        {
            var favori = await _context.Favoris
                .FirstOrDefaultAsync(f => f.UserId == userId && f.BienId == bienId);

            if (favori == null)
                return NotFound(new { success = false, message = "Favori introuvable." });

            _context.Favoris.Remove(favori);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Retiré des favoris." });
        }

        // ─── GET /api/favoris/check/{userId}/{bienId} ─────────────────────────
        // Vérifier si un bien est en favori (utile pour l'état du bouton ❤️)
        [HttpGet("check/{userId}/{bienId}")]
        public async Task<IActionResult> Check(int userId, int bienId)
        {
            var isFav = await _context.Favoris
                .AnyAsync(f => f.UserId == userId && f.BienId == bienId);

            return Ok(new { success = true, isFavorite = isFav });
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────
    public class FavoriDto
    {
        public int      Id        { get; set; }
        public int      UserId    { get; set; }
        public int      BienId    { get; set; }
        public DateTime CreatedAt { get; set; }
        public BienDto? Bien      { get; set; }
    }

    // ─── REQUEST MODEL ────────────────────────────────────────────────────────
    public class AddFavoriRequest
    {
        public int UserId { get; set; }
        public int BienId { get; set; }
    }
}