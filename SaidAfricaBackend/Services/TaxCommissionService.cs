namespace SaidAfricaBackend.Services
{
    /// <summary>
    /// Taux de taxes applicables au Cameroun selon le type de transaction.
    /// Ces pourcentages peuvent être ajustés via la configuration.
    /// </summary>
    public class TauxTaxeConfig
    {
        // Droits d'enregistrement vente immobilière (Cameroun) — 10 %
        public decimal TauxVentePct     { get; set; } = 10m;
        // Taxes sur location (TVA + impôts fonciers) — 15 %
        public decimal TauxLocationPct  { get; set; } = 15m;
    }

    public class ResultatCalculTaxe
    {
        public decimal MontantBrut           { get; set; }
        public decimal TauxTaxePct           { get; set; }
        public decimal MontantTaxe           { get; set; }
        public decimal MontantNetApresImpots { get; set; }
        public decimal CommissionLevetimmo   { get; set; }   // 50 % du net
        public decimal CommissionAgent       { get; set; }   // 50 % du net
        public bool    GereParLevetimmo      { get; set; }
        public string  Explication           { get; set; } = string.Empty;
    }

    public interface ITaxCommissionService
    {
        ResultatCalculTaxe Calculer(decimal montantBrut, string typeTransaction, string typeCompteProf, bool agenceVendTerrain = false);
        bool               AgentEnPeriodeGratuite(DateTime? dateDevenirPro);
        bool               AbonnementActif(DateTime? abonnementExpireLe);
        StatutAbonnement   GetStatutAbonnement(User user);
    }

    public class StatutAbonnement
    {
        public bool   EnPeriodeGratuite  { get; set; }
        public bool   AbonnementPayeActif { get; set; }
        public bool   DoitPayer          { get; set; }   // hors période gratuite ET pas d'abonnement actif
        public int    JoursRestantsGratuit { get; set; }
        public DateTime? ExpirationAbonnement { get; set; }
        public string Message            { get; set; } = string.Empty;
    }

    public class TaxCommissionService : ITaxCommissionService
    {
        private readonly TauxTaxeConfig _config;
        private const int AnneesGratuites = 1;
        private const decimal PartLevetimmo = 0.50m;
        private const decimal PartAgent     = 0.50m;

        // Seuil location directe (< 50 000 XAF → agent direct ; >= 50 000 → Levetimmo)
        private const decimal SeuilLocationDirecte = 50_000m;

        public TaxCommissionService(TauxTaxeConfig config)
        {
            _config = config;
        }

        public ResultatCalculTaxe Calculer(decimal montantBrut, string typeTransaction, string typeCompteProf, bool agenceVendTerrain = false)
        {
            // ── 1. Déterminer si Levetimmo gère la transaction ───────────────────
            bool gereParLevetimmo = DeterminerGestionLevetimmo(montantBrut, typeTransaction, typeCompteProf, agenceVendTerrain);

            // ── 2. Taux de taxe applicable ────────────────────────────────────────
            decimal tauxPct = typeTransaction.ToLower() == "location"
                ? _config.TauxLocationPct
                : _config.TauxVentePct;

            // ── 3. Calculs ────────────────────────────────────────────────────────
            decimal montantTaxe    = Math.Round(montantBrut * tauxPct / 100m, 2);
            decimal montantNet     = montantBrut - montantTaxe;
            decimal commLevetimmo  = gereParLevetimmo ? Math.Round(montantNet * PartLevetimmo, 2) : 0m;
            decimal commAgent      = gereParLevetimmo ? Math.Round(montantNet * PartAgent,     2) : montantNet;

            string explication = BuildExplication(montantBrut, tauxPct, montantTaxe, montantNet,
                                                   commLevetimmo, commAgent, gereParLevetimmo,
                                                   typeTransaction, typeCompteProf, agenceVendTerrain);

            return new ResultatCalculTaxe
            {
                MontantBrut           = montantBrut,
                TauxTaxePct           = tauxPct,
                MontantTaxe           = montantTaxe,
                MontantNetApresImpots = montantNet,
                CommissionLevetimmo   = commLevetimmo,
                CommissionAgent       = commAgent,
                GereParLevetimmo      = gereParLevetimmo,
                Explication           = explication,
            };
        }

        public bool AgentEnPeriodeGratuite(DateTime? dateDevenirPro)
        {
            if (dateDevenirPro == null) return true;
            return DateTime.UtcNow < dateDevenirPro.Value.AddYears(AnneesGratuites);
        }

        public bool AbonnementActif(DateTime? abonnementExpireLe)
        {
            if (abonnementExpireLe == null) return false;
            return DateTime.UtcNow <= abonnementExpireLe.Value;
        }

        public StatutAbonnement GetStatutAbonnement(User user)
        {
            bool gratuit     = AgentEnPeriodeGratuite(user.DateDevenirPro);
            bool abonnActif  = AbonnementActif(user.AbonnementExpireLe);
            bool doitPayer   = !gratuit && !abonnActif;

            int joursRestants = 0;
            if (gratuit && user.DateDevenirPro != null)
            {
                var finGratuit = user.DateDevenirPro.Value.AddYears(AnneesGratuites);
                joursRestants  = Math.Max(0, (int)(finGratuit - DateTime.UtcNow).TotalDays);
            }

            string msg = gratuit
                ? $"Période gratuite — {joursRestants} jour(s) restant(s)"
                : abonnActif
                    ? $"Abonnement actif jusqu'au {user.AbonnementExpireLe!.Value:dd/MM/yyyy}"
                    : "Abonnement expiré — veuillez renouveler votre abonnement annuel pour continuer";

            return new StatutAbonnement
            {
                EnPeriodeGratuite    = gratuit,
                AbonnementPayeActif  = abonnActif,
                DoitPayer            = doitPayer,
                JoursRestantsGratuit = joursRestants,
                ExpirationAbonnement = user.AbonnementExpireLe,
                Message              = msg,
            };
        }

        // ── Règles de routage ─────────────────────────────────────────────────────
        private static bool DeterminerGestionLevetimmo(decimal montant, string typeTransaction, string typeCompteProf, bool agenceVendTerrain)
        {
            string type = typeTransaction.ToLower();

            // AgenceImmobiliere qui vend un terrain → direct (pas de Levetimmo)
            if (typeCompteProf == "AgenceImmobiliere" && type == "vente" && agenceVendTerrain)
                return false;

            // Location < 50 000 XAF → agent direct
            if (type == "location" && montant < SeuilLocationDirecte)
                return false;

            // Tout le reste (location >= 50k, ventes non-agence, promoteurs, propriétaires) → Levetimmo
            return true;
        }

        private static string BuildExplication(decimal brut, decimal taux, decimal taxe, decimal net,
            decimal commLev, decimal commAgent, bool gere, string type, string compteProf, bool agenceTerrain)
        {
            string gestionnaire = gere ? "Levetimmo" : "l'agent directement";
            string regleTrigger = !gere
                ? (compteProf == "AgenceImmobiliere" && agenceTerrain
                    ? "Agence immobilière — vente terrain directe"
                    : $"Location < 50 000 XAF — gestion directe")
                : (type.ToLower() == "location"
                    ? "Location ≥ 50 000 XAF — géré par Levetimmo"
                    : "Vente — géré par Levetimmo");

            return $"Règle appliquée : {regleTrigger}. " +
                   $"Montant brut : {brut:N0} XAF. " +
                   $"Taxe {taux} % : {taxe:N0} XAF. " +
                   $"Net après impôts : {net:N0} XAF. " +
                   $"Transaction gérée par {gestionnaire}. " +
                   (gere ? $"Commission Levetimmo : {commLev:N0} XAF | Commission agent : {commAgent:N0} XAF." : "");
        }
    }
}
