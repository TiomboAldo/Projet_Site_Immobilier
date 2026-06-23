using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SaidAfricaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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
        public String Email { get; set; }
        public String Password { get; set; }
    }
}