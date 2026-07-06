using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SaidAfricaBackend.Services;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _email;

        public AuthController(ApplicationDbContext context, IConfiguration config, IEmailService email)
        {
            _context = context;
            _config = config;
            _email = email;
        }

        private string GenerateJwt(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresMinutes"] ?? "120"));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
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

                _ = _email.SendBienvenueAsync(newUser.Email, newUser.Prenom);

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

        // --- BOOTSTRAP : créer le tout premier compte Admin région ---
        // Ne fonctionne que si aucun compte admin n'existe encore. Se verrouille
        // définitivement (403) dès qu'un premier admin a été créé.
        [HttpPost("bootstrap-admin")]
        public async Task<IActionResult> BootstrapAdmin([FromBody] BootstrapAdminRequest request)
        {
            bool adminExists = await _context.Users.AnyAsync(u =>
                u.Role == "AdminRegion" || u.Role == "AdminPays" || u.Role == "DirecteurProjet");

            if (adminExists)
                return StatusCode(403, new { success = false, message = "Un compte administrateur existe déjà. Ce point d'entrée est désactivé." });

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { success = false, message = "Cet email est déjà utilisé." });

            var admin = new User
            {
                Nom      = request.Nom,
                Prenom   = request.Prenom,
                Email    = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role     = "AdminRegion",
                Region   = request.Region,
                EstValide = true,
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Compte administrateur racine créé. Ce point d'entrée est désormais désactivé." });
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
                var token = GenerateJwt(user);

                return Ok(new
                {
                    success = true,
                    message = $"Ravi de vous revoir, {user.Prenom} !",
                    token,
                    user = new
                    {
                        user.Id,
                        user.Nom,
                        user.Prenom,
                        user.Email,
                        user.Role,
                        user.EstValide
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
        public required String Email { get; set; }
        public required String Password { get; set; }
    }

    public class BootstrapAdminRequest
    {
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }
}