using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("image")]
        [Authorize]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Aucun fichier envoyé." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Max 10 Mo par image." });

            var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
            if (!allowed.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { success = false, message = "Format accepté : JPG, PNG ou WEBP." });

            var dir = Path.Combine(_env.ContentRootPath, "Uploads", "Images");
            Directory.CreateDirectory(dir);

            var ext  = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var filename = $"{Guid.NewGuid()}{ext}";

            using var stream = System.IO.File.Create(Path.Combine(dir, filename));
            await file.CopyToAsync(stream);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return Ok(new { success = true, url = $"{baseUrl}/uploads/images/{filename}" });
        }
    }
}
