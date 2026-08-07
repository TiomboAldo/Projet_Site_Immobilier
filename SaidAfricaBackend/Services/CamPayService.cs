using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SaidAfricaBackend.Services
{
    public interface ICamPayService
    {
        bool IsConfigured { get; }
        Task<(bool success, string reference, string? error)> CollectAsync(
            string phoneNumber, decimal amount, string description, string externalReference);
        Task<string> GetStatusAsync(string reference);
    }

    public class CamPayService : ICamPayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<CamPayService> _logger;

        private string Username  => _config["CamPay:Username"]  ?? "";
        private string Password  => _config["CamPay:Password"]  ?? "";
        private bool   IsSandbox => !string.Equals(_config["CamPay:LiveMode"], "true", StringComparison.OrdinalIgnoreCase);
        private string BaseUrl   => IsSandbox ? "https://demo.campay.net/api" : "https://campay.net/api";

        public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

        public CamPayService(IConfiguration config, IHttpClientFactory http, ILogger<CamPayService> logger)
        {
            _config = config;
            _http   = http;
            _logger = logger;
        }

        private async Task<string?> GetTokenAsync()
        {
            var client = _http.CreateClient();
            var body   = new StringContent(
                JsonSerializer.Serialize(new { username = Username, password = Password }),
                Encoding.UTF8, "application/json");

            var res = await client.PostAsync($"{BaseUrl}/token/", body);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("CamPay token failed: {Status}", res.StatusCode);
                return null;
            }

            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("token").GetString();
        }

        public async Task<(bool success, string reference, string? error)> CollectAsync(
            string phoneNumber, decimal amount, string description, string externalReference)
        {
            if (!IsConfigured)
                return (false, "", "CamPay non configuré — ajoutez CamPay:Username et CamPay:Password.");

            var token = await GetTokenAsync();
            if (token == null)
                return (false, "", "Impossible d'obtenir le token CamPay.");

            var msisdn = new string(phoneNumber.Where(char.IsDigit).ToArray());

            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", token);

            var payload = JsonSerializer.Serialize(new
            {
                amount             = ((int)Math.Ceiling(amount)).ToString(),
                currency           = "XAF",
                from               = msisdn,
                description,
                external_reference = externalReference,
            });

            _logger.LogInformation("CamPay collect → MSISDN:{MSISDN} Amount:{Amt} Ext:{Ext}",
                msisdn, (int)Math.Ceiling(amount), externalReference);

            var res  = await client.PostAsync($"{BaseUrl}/collect/",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("CamPay collect failed: {Status} {Body}", res.StatusCode, body);
                return (false, "", $"Erreur CamPay ({(int)res.StatusCode}): {body}");
            }

            var doc       = JsonDocument.Parse(body);
            var reference = doc.RootElement.GetProperty("reference").GetString() ?? "";
            _logger.LogInformation("CamPay payment initiated: {Ref}", reference);
            return (true, reference, null);
        }

        public async Task<string> GetStatusAsync(string reference)
        {
            if (!IsConfigured) return "UNCONFIGURED";

            var token = await GetTokenAsync();
            if (token == null) return "ERROR";

            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", token);

            var res = await client.GetAsync($"{BaseUrl}/transaction/{reference}/");
            if (!res.IsSuccessStatusCode) return "ERROR";

            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("status", out var s)
                ? s.GetString() ?? "PENDING"
                : "PENDING";
        }
    }
}
