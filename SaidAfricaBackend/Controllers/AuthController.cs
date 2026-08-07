using Microsoft.AspNetCore.Authorization;
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

        private int CurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // --- MON PROFIL : GET ---
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound(new { success = false });
            return Ok(new
            {
                success = true,
                data = new { user.Id, user.Nom, user.Prenom, user.Email, user.Telephone, user.PhotoUrl, user.Role, user.EstBloque }
            });
        }

        // --- MON PROFIL : PUT ---
        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound(new { success = false });

            user.Nom       = req.Nom?.Trim()       ?? user.Nom;
            user.Prenom    = req.Prenom?.Trim()     ?? user.Prenom;
            user.Telephone = req.Telephone?.Trim()  ?? user.Telephone;
            if (req.PhotoUrl != null) user.PhotoUrl = req.PhotoUrl.Trim();

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Profil mis à jour." });
        }

        // --- CHANGEMENT DE MOT DE PASSE ---
        [HttpPut("changepassword")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound(new { success = false });

            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.Password))
                return BadRequest(new { success = false, message = "Mot de passe actuel incorrect." });

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return BadRequest(new { success = false, message = "Le nouveau mot de passe doit contenir au moins 6 caractères." });

            user.Password = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Mot de passe modifié avec succès." });
        }

        // --- MOT DE PASSE OUBLIÉ ---
        [HttpPost("forgotpassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { success = false, message = "Email requis." });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.Trim().ToLower());

            if (user == null)
                return Ok(new { success = true, devToken = (string?)null });

            user.ResetToken       = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            bool smtpConfigured = !string.IsNullOrEmpty(_config["Smtp:Username"]);
            _ = _email.SendPasswordResetAsync(user.Email, user.Prenom, user.ResetToken);

            // Si SMTP actif → email envoyé, on ne renvoie pas le token (l'utilisateur consulte sa boîte)
            // Sinon → token retourné directement pour permettre la réinitialisation sans email
            return Ok(new { success = true, devToken = smtpConfigured ? null : user.ResetToken });
        }

        [HttpPost("resetpassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { success = false, message = "Données manquantes." });

            if (req.NewPassword.Length < 6)
                return BadRequest(new { success = false, message = "Le mot de passe doit contenir au moins 6 caractères." });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == req.Token && u.ResetTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return BadRequest(new { success = false, message = "Lien invalide ou expiré. Veuillez refaire une demande." });

            user.Password         = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            user.ResetToken       = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Mot de passe réinitialisé avec succès. Vous pouvez maintenant vous connecter." });
        }

        // --- BLOQUER / DÉBLOQUER UN COMPTE ---
        [HttpPut("users/{id}/toggle-bloc")]
        [Authorize(Roles = "AdminRegion,AdminPays,DirecteurProjet")]
        public async Task<IActionResult> ToggleBloc(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { success = false, message = "Utilisateur introuvable." });
            user.EstBloque = !user.EstBloque;
            await _context.SaveChangesAsync();
            return Ok(new
            {
                success   = true,
                estBloque = user.EstBloque,
                message   = user.EstBloque ? "Compte suspendu avec succès." : "Compte réactivé avec succès."
            });
        }

        // --- CONNEXION ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                    return Unauthorized(new { success = false, message = "Email ou mot de passe incorrect." });

                // OTP obligatoire à chaque connexion
                var otp       = new Random().Next(100000, 999999).ToString();
                var tempToken = Guid.NewGuid().ToString("N");

                user.TwoFactorOtp       = otp;
                user.TwoFactorOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                user.TwoFactorTempToken = tempToken;
                await _context.SaveChangesAsync();

                bool emailSent = false;
                try { await _email.SendTwoFactorOtpAsync(user.Email, user.Prenom, otp); emailSent = true; }
                catch { /* email non critique */ }

                return Ok(new
                {
                    success           = true,
                    requiresTwoFactor = true,
                    tempToken,
                    devOtp = emailSent ? null : otp
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Erreur : " + ex.Message });
            }
        }

        // --- VÉRIFIER LE CODE OTP (étape 2 de la connexion) ---
        [HttpPost("verify-2fa")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest req)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.TwoFactorTempToken == req.TempToken &&
                u.TwoFactorOtpExpiry > DateTime.UtcNow);

            if (user == null)
                return BadRequest(new { success = false, message = "Session expirée. Veuillez vous reconnecter." });

            if (user.TwoFactorOtp != req.Code)
                return BadRequest(new { success = false, message = "Code incorrect." });

            user.TwoFactorOtp       = null;
            user.TwoFactorOtpExpiry = null;
            user.TwoFactorTempToken = null;
            await _context.SaveChangesAsync();

            var token = GenerateJwt(user);
            return Ok(new
            {
                success = true,
                message = $"Ravi de vous revoir, {user.Prenom} !",
                token,
                user = new { user.Id, user.Nom, user.Prenom, user.Email, user.Role, user.EstValide, user.EstBloque }
            });
        }

        // --- STATUT 2FA ---
        [HttpGet("2fa-status")]
        [Authorize]
        public async Task<IActionResult> GetTwoFactorStatus()
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();
            return Ok(new { success = true, twoFactorEnabled = user.TwoFactorEnabled });
        }

        // --- ACTIVER LA 2FA : demande d'OTP ---
        [HttpPost("request-enable-2fa")]
        [Authorize]
        public async Task<IActionResult> RequestEnableTwoFactor()
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();

            if (user.TwoFactorEnabled)
                return BadRequest(new { success = false, message = "La 2FA est déjà activée." });

            var otp = new Random().Next(100000, 999999).ToString();
            user.TwoFactorOtp       = otp;
            user.TwoFactorOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _context.SaveChangesAsync();

            bool emailSent = false;
            try { await _email.SendTwoFactorOtpAsync(user.Email, user.Prenom, otp); emailSent = true; }
            catch { /* email non critique */ }

            return Ok(new { success = true, devOtp = emailSent ? null : otp });
        }

        // --- ACTIVER LA 2FA : confirmer l'OTP ---
        [HttpPost("confirm-enable-2fa")]
        [Authorize]
        public async Task<IActionResult> ConfirmEnableTwoFactor([FromBody] ConfirmTwoFactorRequest req)
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();

            if (user.TwoFactorOtp != req.Code || user.TwoFactorOtpExpiry < DateTime.UtcNow)
                return BadRequest(new { success = false, message = "Code incorrect ou expiré." });

            user.TwoFactorEnabled   = true;
            user.TwoFactorOtp       = null;
            user.TwoFactorOtpExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Authentification à deux facteurs activée avec succès." });
        }

        // --- DÉSACTIVER LA 2FA ---
        [HttpPost("disable-2fa")]
        [Authorize]
        public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest req)
        {
            var user = await _context.Users.FindAsync(CurrentUserId());
            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
                return BadRequest(new { success = false, message = "Mot de passe incorrect." });

            user.TwoFactorEnabled   = false;
            user.TwoFactorOtp       = null;
            user.TwoFactorOtpExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Authentification à deux facteurs désactivée." });
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

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword     { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string? Nom       { get; set; }
        public string? Prenom    { get; set; }
        public string? Telephone { get; set; }
        public string? PhotoUrl  { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Token       { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class VerifyTwoFactorRequest
    {
        public string TempToken { get; set; } = string.Empty;
        public string Code      { get; set; } = string.Empty;
    }

    public class ConfirmTwoFactorRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class DisableTwoFactorRequest
    {
        public string Password { get; set; } = string.Empty;
    }
}