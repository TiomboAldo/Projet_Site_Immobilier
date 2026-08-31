# Levetimmo — Plateforme immobilière camerounaise

Site en production : **levetimmo.com** | Hébergé sur Railway

## Documentation du projet

| Fichier | Contenu |
|---|---|
| [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) | Stack technique, architecture, structure des fichiers, rôles utilisateurs, modèles de données |
| [PROJECT_FEATURES.md](PROJECT_FEATURES.md) | Toutes les fonctionnalités implémentées en production (état au 2026-08-31) |
| [PROJECT_BUSINESS_RULES.md](PROJECT_BUSINESS_RULES.md) | Règles métier : commissions, abonnements, routage des transactions, modération |
| [PROJECT_ROADMAP.md](PROJECT_ROADMAP.md) | Ce qui reste à construire — 3 sprints classés par priorité (analyse vs njikam.com) |

## Démarrage rapide

### Backend (ASP.NET Core 10)
```bash
cd SaidAfricaBackend
cp .env.example .env   # remplir les variables
dotnet run
```

### Frontend
```bash
npm install
npm run dev
```

### Build complet (comme Railway)
```bash
npm run build          # génère dist/
cd SaidAfricaBackend
dotnet publish -c Release -o /app/publish
```

## Variables d'environnement requises (Railway)

| Variable | Description |
|---|---|
| `MYSQLHOST` / `MYSQLPORT` / `MYSQLUSER` / `MYSQLPASSWORD` / `MYSQLDATABASE` | Connexion MySQL Railway |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | Clés JWT |
| `Smtp__Host` / `Smtp__Port` / `Smtp__Username` / `Smtp__Password` | SMTP email |
| `Google__ClientId` | OAuth Google |
| `MoMo__SubscriptionKey` / `MoMo__ApiUser` / `MoMo__ApiKey` | MTN Mobile Money |
