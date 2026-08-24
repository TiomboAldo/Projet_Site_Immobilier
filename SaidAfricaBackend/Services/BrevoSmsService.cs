using System.Text;
using System.Text.Json;

namespace SaidAfricaBackend.Services
{
    public interface ISmsService
    {
        bool IsConfigured { get; }
        Task<bool> SendOtpAsync(string phoneNumber, string code);
    }

    public class BrevoSmsService : ISmsService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<BrevoSmsService> _logger;

        private string ApiKey => _config["Brevo:ApiKey"] ?? "";
        private string Sender => _config["Brevo:SmsSender"] ?? "Levetimmo";
        public bool IsConfigured => !string.IsNullOrEmpty(ApiKey);

        public BrevoSmsService(IConfiguration config, IHttpClientFactory http, ILogger<BrevoSmsService> logger)
        {
            _config = config;
            _http   = http;
            _logger = logger;
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string code)
        {
            if (!IsConfigured) return false;

            var phone = phoneNumber.Trim();
            if (!phone.StartsWith("+"))
                phone = phone.StartsWith("237") ? "+" + phone : "+237" + phone.TrimStart('0');

            var client  = _http.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", ApiKey);

            var payload = JsonSerializer.Serialize(new
            {
                sender    = Sender,
                recipient = phone,
                content   = $"Levetimmo - Code de verification : {code}. Valable 10 minutes.",
            });

            try
            {
                var res  = await client.PostAsync(
                    "https://api.brevo.com/v3/transactionalSMS/sms",
                    new StringContent(payload, Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Brevo SMS echec: {Status} {Body}", res.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("Brevo SMS envoye a {Phone}: {Body}", phone, body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Brevo SMS erreur pour {Phone}", phone);
                return false;
            }
        }
    }
}
