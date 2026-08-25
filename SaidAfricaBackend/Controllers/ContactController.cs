using Microsoft.AspNetCore.Mvc;
using SaidAfricaBackend.Services;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _email;

        public ContactController(IEmailService email)
        {
            _email = email;
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] ContactRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Nom) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { success = false, message = "Tous les champs sont requis." });

            try
            {
                await _email.SendContactAsync(req.Nom, req.Email, req.Message);
                return Ok(new { success = true, message = "Message envoyé. Nous vous répondrons sous 24h." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Erreur envoi email : " + ex.Message });
            }
        }
    }

    public class ContactRequest
    {
        public string Nom     { get; set; } = string.Empty;
        public string Email   { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
