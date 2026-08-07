using System.Text;
using System.Text.Json;

namespace SaidAfricaBackend.Services
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
        Task SendReservationConfirmeeAsync(string clientEmail, string clientPrenom, string bienTitre, string dateVisite, string prix);
        Task SendReservationRefuseeAsync(string clientEmail, string clientPrenom, string bienTitre, string? messageProprietaire);
        Task SendNouvelleReservationAsync(string proprietaireEmail, string proprietairePrenom, string bienTitre, string clientPrenom, string clientNom, string dateVisite);
        Task SendPaiementConfirmeProprietaireAsync(string proprietaireEmail, string proprietairePrenom, string bienTitre, string clientPrenom, string clientNom, decimal montant, string dateVisite);
        Task SendNouveauMessageAsync(string destinataireEmail, string destinatairePrenom, string expediteurPrenom, string bienTitre, string apercu);
        Task SendDemandeProprietaireValideeAsync(string clientEmail, string clientPrenom);
        Task SendDemandeProprietaireRefuseeAsync(string clientEmail, string clientPrenom, string? motif);
        Task SendBienvenueAsync(string email, string prenom);
        Task SendPasswordResetAsync(string email, string prenom, string resetToken);
        Task SendTwoFactorOtpAsync(string email, string prenom, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ─── Envoi générique via Brevo API ───────────────────────────────────
        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var apiKey   = _config["Brevo:ApiKey"]    ?? "";
            var fromName = _config["Smtp:FromName"]   ?? "Levetimmo";
            var fromMail = _config["Smtp:FromEmail"]  ?? "tiomboaldo@gmail.com";

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Brevo:ApiKey non configurée.");

            var payload = new
            {
                sender     = new { name = fromName, email = fromMail },
                to         = new[] { new { email = toEmail, name = toName } },
                subject,
                htmlContent = WrapTemplate(subject, htmlBody)
            };

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("api-key", apiKey);
            var content  = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://api.brevo.com/v3/smtp/email", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Brevo {response.StatusCode}: {err}");
            }

            _logger.LogInformation("Email envoyé via Brevo à {Email} : {Subject}", toEmail, subject);
        }

        // ─── Emails métier ────────────────────────────────────────────────────

        public Task SendReservationConfirmeeAsync(string clientEmail, string clientPrenom, string bienTitre, string dateVisite, string prix)
            => SendAsync(clientEmail, clientPrenom,
                $"✅ Votre visite est confirmée — {bienTitre}",
                $@"<h2>Bonjour {clientPrenom} 👋</h2>
                   <p>Bonne nouvelle ! Votre demande de visite pour le bien <strong>{bienTitre}</strong> a été <strong style='color:#16a34a;'>acceptée</strong> par le propriétaire.</p>
                   <div style='background:#f0fdf4;border-left:4px solid #16a34a;padding:16px;border-radius:8px;margin:20px 0;'>
                       <p style='margin:0;'><strong>Bien :</strong> {bienTitre}</p>
                       <p style='margin:8px 0 0;'><strong>Date de visite :</strong> {dateVisite}</p>
                       <p style='margin:8px 0 0;'><strong>Prix :</strong> {prix}</p>
                   </div>
                   <p>Connectez-vous à votre espace client pour communiquer avec le propriétaire ou ajuster les détails.</p>
                   <a href='https://levetimmo.com/src/pages/accueil_user.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Voir mon espace</a>");

        public Task SendReservationRefuseeAsync(string clientEmail, string clientPrenom, string bienTitre, string? messageProprietaire)
            => SendAsync(clientEmail, clientPrenom,
                $"❌ Demande de visite refusée — {bienTitre}",
                $@"<h2>Bonjour {clientPrenom},</h2>
                   <p>Votre demande de visite pour le bien <strong>{bienTitre}</strong> n'a malheureusement pas pu être acceptée.</p>
                   {(string.IsNullOrEmpty(messageProprietaire) ? "" : $"<div style='background:#fef2f2;border-left:4px solid #ef4444;padding:16px;border-radius:8px;margin:20px 0;'><p style='margin:0;'><em>Message du propriétaire : {messageProprietaire}</em></p></div>")}
                   <p>D'autres biens similaires sont disponibles sur notre plateforme.</p>
                   <a href='https://levetimmo.com/src/pages/accueil_user.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Explorer les biens</a>");

        public Task SendNouvelleReservationAsync(string proprietaireEmail, string proprietairePrenom, string bienTitre, string clientPrenom, string clientNom, string dateVisite)
            => SendAsync(proprietaireEmail, proprietairePrenom,
                $"🏠 Nouvelle demande de visite — {bienTitre}",
                $@"<h2>Bonjour {proprietairePrenom},</h2>
                   <p><strong>{clientPrenom} {clientNom}</strong> souhaite visiter votre bien <strong>{bienTitre}</strong>.</p>
                   <div style='background:#eff6ff;border-left:4px solid #2563eb;padding:16px;border-radius:8px;margin:20px 0;'>
                       <p style='margin:0;'><strong>Client :</strong> {clientPrenom} {clientNom}</p>
                       <p style='margin:8px 0 0;'><strong>Date souhaitée :</strong> {dateVisite}</p>
                       <p style='margin:8px 0 0;'><strong>Bien :</strong> {bienTitre}</p>
                   </div>
                   <p>Rendez-vous dans votre espace propriétaire pour accepter ou refuser cette demande.</p>
                   <a href='https://levetimmo.com/src/pages/espace_proprietaire.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Gérer mes réservations</a>");

        public Task SendPaiementConfirmeProprietaireAsync(string proprietaireEmail, string proprietairePrenom, string bienTitre, string clientPrenom, string clientNom, decimal montant, string dateVisite)
            => SendAsync(proprietaireEmail, proprietairePrenom,
                $"✅ Paiement reçu — visite de {bienTitre}",
                $@"<h2>Bonjour {proprietairePrenom},</h2>
                   <p>Bonne nouvelle ! <strong>{clientPrenom} {clientNom}</strong> a payé les frais de visite pour votre bien <strong>{bienTitre}</strong>.</p>
                   <div style='background:#f0fdf4;border-left:4px solid #16a34a;padding:16px;border-radius:8px;margin:20px 0;'>
                       <p style='margin:0;'><strong>Client :</strong> {clientPrenom} {clientNom}</p>
                       <p style='margin:8px 0 0;'><strong>Date souhaitée :</strong> {dateVisite}</p>
                       <p style='margin:8px 0 0;'><strong>Bien :</strong> {bienTitre}</p>
                       <p style='margin:8px 0 0;'><strong>Montant payé :</strong> {montant:N0} XAF</p>
                   </div>
                   <p>Rendez-vous dans votre espace propriétaire pour confirmer ou refuser la visite.</p>
                   <a href='https://levetimmo.com/src/pages/espace_proprietaire.html' style='display:inline-block;background:#16a34a;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Gérer mes réservations</a>");

        public Task SendNouveauMessageAsync(string destinataireEmail, string destinatairePrenom, string expediteurPrenom, string bienTitre, string apercu)
            => SendAsync(destinataireEmail, destinatairePrenom,
                $"💬 Nouveau message de {expediteurPrenom}",
                $@"<h2>Bonjour {destinatairePrenom},</h2>
                   <p><strong>{expediteurPrenom}</strong> vous a envoyé un message concernant <strong>{bienTitre}</strong>.</p>
                   <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin:20px 0;font-style:italic;color:#475569;'>
                       « {apercu}{(apercu.Length >= 120 ? "…" : "")} »
                   </div>
                   <p>Répondez directement depuis votre espace.</p>
                   <a href='https://levetimmo.com/src/pages/accueil_user.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Voir la messagerie</a>");

        public Task SendDemandeProprietaireValideeAsync(string clientEmail, string clientPrenom)
            => SendAsync(clientEmail, clientPrenom,
                "🎉 Votre compte Propriétaire est activé — Levetimmo",
                $@"<h2>Félicitations {clientPrenom} !</h2>
                   <p>Votre demande pour devenir propriétaire sur <strong>Levetimmo</strong> a été <strong style='color:#16a34a;'>approuvée</strong>.</p>
                   <p>Vous pouvez maintenant publier vos annonces, gérer vos réservations et communiquer avec vos clients.</p>
                   <a href='https://levetimmo.com/src/pages/espace_proprietaire.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Accéder à mon espace propriétaire</a>");

        public Task SendDemandeProprietaireRefuseeAsync(string clientEmail, string clientPrenom, string? motif)
            => SendAsync(clientEmail, clientPrenom,
                "❌ Demande propriétaire refusée — Levetimmo",
                $@"<h2>Bonjour {clientPrenom},</h2>
                   <p>Votre demande pour devenir propriétaire sur Levetimmo n'a pas pu être acceptée pour le moment.</p>
                   {(string.IsNullOrEmpty(motif) ? "" : $"<div style='background:#fef2f2;border-left:4px solid #ef4444;padding:16px;border-radius:8px;margin:20px 0;'><p style='margin:0;'><strong>Motif :</strong> {motif}</p></div>")}
                   <p>Vous pouvez soumettre une nouvelle demande depuis votre espace client.</p>
                   <a href='https://levetimmo.com/src/pages/accueil_user.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Mon espace client</a>");

        public Task SendBienvenueAsync(string email, string prenom)
            => SendAsync(email, prenom,
                "🎉 Bienvenue sur Levetimmo !",
                $@"<h2>Bienvenue, {prenom} ! 🎉</h2>
                   <p>Nous sommes ravis de vous compter parmi les membres de <strong>Levetimmo</strong>, la plateforme immobilière premium dédiée à l'Afrique.</p>
                   <div style='background:#eff6ff;border-left:4px solid #2563eb;padding:16px;border-radius:8px;margin:20px 0;'>
                       <p style='margin:0;font-weight:700;'>Avec votre compte vous pouvez :</p>
                       <ul style='margin:12px 0 0;padding-left:20px;'>
                           <li>Parcourir des centaines de biens immobiliers</li>
                           <li>Envoyer des demandes de visite en un clic</li>
                           <li>Communiquer directement avec les propriétaires</li>
                           <li>Sauvegarder vos biens favoris</li>
                       </ul>
                   </div>
                   <p>Commencez dès maintenant à explorer les biens disponibles !</p>
                   <a href='https://levetimmo.com/src/pages/accueil_user.html' style='display:inline-block;background:#2563eb;color:#fff;padding:12px 28px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:8px;'>Explorer les biens</a>
                   <p style='margin-top:24px;font-size:13px;color:#6b7280;'>Si vous n'avez pas créé ce compte, ignorez cet email.</p>");

        public Task SendTwoFactorOtpAsync(string email, string prenom, string otp)
            => SendAsync(email, prenom,
                "🔐 Votre code de vérification — Levetimmo",
                $@"<h2>Bonjour {prenom},</h2>
                   <p>Voici votre code de vérification pour finaliser votre connexion sur <strong>Levetimmo</strong>.</p>
                   <div style='text-align:center;margin:28px 0;'>
                       <div style='display:inline-block;background:#eff6ff;border:2px solid #2563eb;border-radius:16px;padding:20px 36px;'>
                           <p style='margin:0;font-size:36px;font-weight:900;letter-spacing:10px;color:#1e40af;'>{otp}</p>
                       </div>
                   </div>
                   <p style='color:#6b7280;font-size:13px;'>Ce code est valable <strong>10 minutes</strong>. Ne le partagez avec personne.</p>
                   <p style='color:#6b7280;font-size:13px;'>Si vous n'êtes pas à l'origine de cette tentative de connexion, changez votre mot de passe immédiatement.</p>");

        public Task SendPasswordResetAsync(string email, string prenom, string resetToken)
        {
            var link = $"https://levetimmo.com/src/pages/login.html?token={resetToken}";
            return SendAsync(email, prenom,
                "🔐 Réinitialisation de votre mot de passe — Levetimmo",
                $@"<h2>Bonjour {prenom},</h2>
                   <p>Vous avez demandé à réinitialiser votre mot de passe sur <strong>Levetimmo</strong>.</p>
                   <p>Cliquez sur le bouton ci-dessous pour choisir un nouveau mot de passe. Ce lien est valable <strong>1 heure</strong>.</p>
                   <a href='{link}' style='display:inline-block;background:#2563eb;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:700;margin:20px 0;font-size:15px;'>Réinitialiser mon mot de passe</a>
                   <p style='font-size:13px;color:#6b7280;margin-top:16px;'>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email — votre mot de passe ne sera pas modifié.</p>");
        }

        // ─── Template HTML commun ─────────────────────────────────────────────
        private static string WrapTemplate(string subject, string content) => $@"
<!DOCTYPE html><html lang='fr'><head><meta charset='UTF-8'>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; background:#f3f4f6; margin:0; padding:0; }}
  .wrapper {{ max-width:580px; margin:32px auto; background:#fff; border-radius:16px; overflow:hidden; box-shadow:0 4px 24px rgba(0,0,0,0.08); }}
  .header {{ background:linear-gradient(135deg,#1e40af,#2563eb); padding:28px 32px; }}
  .header img {{ height:36px; }}
  .header h1 {{ color:#fff; font-size:20px; margin:12px 0 0; font-weight:800; }}
  .body {{ padding:32px; color:#374151; line-height:1.7; font-size:15px; }}
  .body h2 {{ font-size:18px; font-weight:800; color:#111827; margin-top:0; }}
  .footer {{ background:#f9fafb; padding:20px 32px; text-align:center; font-size:12px; color:#9ca3af; border-top:1px solid #f3f4f6; }}
</style></head>
<body><div class='wrapper'>
  <div class='header'>
    <h1>🏠 Levetimmo</h1>
  </div>
  <div class='body'>{content}</div>
  <div class='footer'>
    © {DateTime.UtcNow.Year} Levetimmo — Plateforme immobilière premium<br>
    Cet email a été envoyé automatiquement, merci de ne pas y répondre directement.
  </div>
</div></body></html>";
    }
}
