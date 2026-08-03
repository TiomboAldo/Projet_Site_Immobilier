using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SaidAfricaBackend.Services
{
    public interface IMoMoService
    {
        bool IsConfigured { get; }
        Task<(bool success, string referenceId, string? error)> InitiateCollectionAsync(
            string phoneNumber, decimal amount, string description, string externalId);
        Task<string> GetPaymentStatusAsync(string referenceId);
    }

    public class MoMoService : IMoMoService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<MoMoService> _logger;

        private string SubscriptionKey => _config["MoMo:SubscriptionKey"] ?? "";
        private string ApiUser         => _config["MoMo:ApiUser"]         ?? "";
        private string ApiKey          => _config["MoMo:ApiKey"]          ?? "";
        private bool   IsSandbox       => string.Equals(_config["MoMo:IsSandbox"], "true", StringComparison.OrdinalIgnoreCase);
        // Le sandbox MTN n'accepte que EUR; XAF est pour la production
        private string Currency        => IsSandbox ? "EUR" : (_config["MoMo:Currency"] ?? "XAF");
        private string BaseUrl         => IsSandbox
            ? "https://sandbox.momodeveloper.mtn.com"
            : "https://proxy.momoapi.mtn.com";

        public bool IsConfigured =>
            !string.IsNullOrEmpty(SubscriptionKey) &&
            !string.IsNullOrEmpty(ApiUser) &&
            !string.IsNullOrEmpty(ApiKey);

        public MoMoService(IConfiguration config, IHttpClientFactory http, ILogger<MoMoService> logger)
        {
            _config = config;
            _http   = http;
            _logger = logger;
        }

        // ─── Obtenir un token Bearer depuis MTN MoMo ─────────────────────────
        private async Task<string?> GetBearerTokenAsync()
        {
            var client = _http.CreateClient();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiUser}:{ApiKey}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

            var res = await client.PostAsync($"{BaseUrl}/collection/token/", null);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("MoMo token failed: {Status}", res.StatusCode);
                return null;
            }

            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("access_token").GetString();
        }

        // ─── Initier une demande de paiement ─────────────────────────────────
        public async Task<(bool success, string referenceId, string? error)> InitiateCollectionAsync(
            string phoneNumber, decimal amount, string description, string externalId)
        {
            if (!IsConfigured)
                return (false, string.Empty, "MoMo non configuré — ajoutez les credentials dans .env");

            var token = await GetBearerTokenAsync();
            if (token == null)
                return (false, string.Empty, "Impossible d'obtenir le token MTN MoMo.");

            var referenceId = Guid.NewGuid().ToString();
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            client.DefaultRequestHeaders.Add("X-Reference-Id", referenceId);
            client.DefaultRequestHeaders.Add("X-Target-Environment",
                IsSandbox ? "sandbox" : "mtnCameroon");

            // Normaliser le numéro : chiffres uniquement, sans + ni espaces (format MSISDN)
            var msisdn = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // Description sans caractères spéciaux (certaines API rejettent les tirets em)
            var safeDesc = description.Replace("—", "-").Replace("–", "-");

            // callbackUrl uniquement en production avec une vraie URL publique
            var callbackUrl = _config["MoMo:CallbackUrl"] ?? "";
            var hasCallback = !IsSandbox
                && callbackUrl.StartsWith("https://")
                && !callbackUrl.Contains("votre-domaine");

            // JsonObject garantit un JSON camelCase propre sans ambiguïté de sérialisation
            var bodyNode = new JsonObject
            {
                ["amount"]       = ((int)Math.Ceiling(amount)).ToString(),
                ["currency"]     = Currency,
                ["externalId"]   = externalId,
                ["payer"]        = new JsonObject
                {
                    ["partyIdType"] = "MSISDN",
                    ["partyId"]     = msisdn,
                },
                ["payerMessage"] = safeDesc,
                ["payeeNote"]    = safeDesc,
            };
            if (hasCallback) bodyNode["callbackUrl"] = callbackUrl;

            var jsonStr = bodyNode.ToJsonString();
            _logger.LogInformation("MoMo request → MSISDN:{MSISDN} Amount:{Amt} Currency:{Cur} Body:{Body}",
                msisdn, (int)Math.Ceiling(amount), Currency, jsonStr);

            var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");

            var res = await client.PostAsync($"{BaseUrl}/collection/v1_0/requesttopay", content);
            if (res.IsSuccessStatusCode) // 202 Accepted
            {
                _logger.LogInformation("MoMo payment initiated: {Ref}", referenceId);
                return (true, referenceId, null);
            }

            var err = await res.Content.ReadAsStringAsync();
            _logger.LogError("MoMo initiation failed: {Status} {Body}", res.StatusCode, err);
            return (false, referenceId, $"Erreur MTN MoMo ({(int)res.StatusCode}): {err} | Sent: {jsonStr}");
        }

        // ─── Vérifier le statut d'un paiement ────────────────────────────────
        public async Task<string> GetPaymentStatusAsync(string referenceId)
        {
            if (!IsConfigured) return "UNCONFIGURED";

            var token = await GetBearerTokenAsync();
            if (token == null) return "ERROR";

            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            client.DefaultRequestHeaders.Add("X-Target-Environment",
                IsSandbox ? "sandbox" : "mtnCameroon");

            var res = await client.GetAsync(
                $"{BaseUrl}/collection/v1_0/requesttopay/{referenceId}");

            if (!res.IsSuccessStatusCode) return "ERROR";

            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("status", out var s)
                ? s.GetString() ?? "PENDING"
                : "PENDING";
            // MTN renvoie : "SUCCESSFUL" | "FAILED" | "PENDING"
        }
    }
}
