using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- INSCRIPTION ---
        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
                if (userExists)
                {
                    return BadRequest(new { success = false, message = "Cet email est déjà utilisé." });
                }

                string passwordHashed = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var newUser = new User
                {
                    Nom = request.Nom,
                    Prenom = request.Prenom,
                    Email = request.Email,
                    Password = passwordHashed
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Inscription réussie et sécurisée !"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Erreur : " + ex.Message });
            }
        }

        // --- CONNEXION ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                // 1. Chercher l'utilisateur par son email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                // 2. Vérifier si l'utilisateur existe ET si le mot de passe correspond
                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                {
                    return Unauthorized(new { success = false, message = "Email ou mot de passe incorrect." });
                }

                // 3. Succès !
                return Ok(new
                {
                    success = true,
                    message = $"Ravi de vous revoir, {user.Prenom} !",
                    user = new
                    {
                        user.Id,
                        user.Nom,
                        user.Prenom,
                        user.Email,
                        user.Role   // ← champ Role maintenant incluS
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Erreur : " + ex.Message });
            }
        }
    }

    // Modèles pour les requêtes
    public class SignUpRequest
    {
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginRequest
    {
        public String Email { get; set; }
        public String Password { get; set; }
    }
}