---
name: project-overview
description: "Vue d'ensemble complète du projet Levetimmo — stack, architecture, fonctionnalités, état actuel"
metadata: 
  node_type: memory
  type: project
  originSessionId: 40b1ed6f-e44c-47c0-b6dc-fc1979ad94a7
  modified: 2026-08-31T08:31:21.412Z
---

# Levetimmo — Plateforme immobilière camerounaise

## Identité du projet
- **Nom commercial :** Levetimmo (anciennement Said Africa en interne)
- **Domaine en production :** levetimmo.com
- **Hébergement :** Railway (backend + frontend servi en statique via wwwroot)
- **Base de données :** MySQL 8 sur Railway
- **Propriétaire / développeur :** Tiombo Aldo (tiomboaldo@gmail.com)
- **Concurrent principal analysé :** njikam.com (plateforme #1 Cameroun)

## Stack technique

### Backend
- **Langage :** C# / ASP.NET Core 10
- **ORM :** Entity Framework Core 8 avec MySQL (Pomelo)
- **Auth :** JWT Bearer + Google OAuth (GSI) + 2FA email OTP
- **Paiement :** MTN Mobile Money (CamPay) — Orange Money à venir
- **SMS :** Brevo SMS Service
- **Email :** SMTP Gmail (tiomboaldo@gmail.com) via EmailService
- **Fichiers :** uploads locaux dans /Uploads (Kyc/, Biens/, Demandes/)

### Frontend
- **Type :** Multi-pages HTML/CSS/JS vanilla (pas de framework)
- **CSS :** Tailwind CSS (via CDN ou build Vite)
- **Build :** Vite + npm
- **Carte :** MapLibre GL JS (carte interactive sur page détail)
- **Icônes :** FontAwesome local
- **Animations :** AOS (Animate On Scroll)

### Déploiement
- **Dockerfile multi-stage :** node:20-alpine (build frontend) → sdk:10 (build backend) → aspnet:10 (runtime)
- **CI/CD :** Push sur `main` → Railway redéploie automatiquement
- **Migrations :** `db.Database.Migrate()` au démarrage + blocs ADO.NET idempotents en fallback

## Structure des fichiers principaux

```
/
├── index.html                          ← Page d'accueil publique
├── src/pages/
│   ├── biens.html                      ← Catalogue des biens
│   ├── details.html                    ← Fiche détaillée d'un bien
│   ├── login.html                      ← Connexion / inscription
│   ├── services.html                   ← Page services
│   ├── apropos.html                    ← Page à propos
│   ├── accueil_user.html               ← Espace utilisateur connecté
│   ├── espace_proprietaire.html        ← Espace propriétaire/professionnel
│   ├── admin_biens_proprietaire.html   ← Gestion biens (propriétaire)
│   └── espace_admin_region.html        ← Tableau de bord admin
├── SaidAfricaBackend/
│   ├── ApplicationDbContext.cs         ← Modèles + DbSets EF
│   ├── Program.cs                      ← Configuration, DI, migrations auto
│   ├── Controllers/
│   │   ├── AuthController.cs           ← Login, register, Google, 2FA, reset MDP
│   │   ├── BiensController.cs          ← CRUD biens + modération
│   │   ├── ReservationsController.cs   ← Demandes de visite
│   │   ├── PaymentsController.cs       ← MTN MoMo
│   │   ├── TransactionsController.cs   ← Commissions + abonnement agent
│   │   ├── KycController.cs            ← Vérification identité
│   │   ├── DemandesProprietaireController.cs ← Demandes comptes pro
│   │   ├── FavorisController.cs
│   │   ├── CommentairesController.cs
│   │   ├── AvisController.cs
│   │   ├── MessagesController.cs
│   │   ├── NotificationsController.cs
│   │   ├── DisponibilitesController.cs
│   │   ├── RecommandationsController.cs
│   │   ├── NewsletterController.cs
│   │   ├── ContactController.cs
│   │   └── UploadController.cs
│   └── Services/
│       ├── EmailService.cs
│       ├── CamPayService.cs            ← MTN MoMo
│       ├── BrevoSmsService.cs
│       ├── MoMoService.cs
│       └── TaxCommissionService.cs     ← Calcul taxes + commissions
```

## Modèles de données (tables en base)

| Table | Description |
|---|---|
| Users | Utilisateurs (tous rôles) |
| Biens | Annonces immobilières |
| Reservations | Demandes de visite |
| Favoris | Biens favoris par utilisateur |
| DemandesProprietaire | Demandes de compte professionnel |
| Commentaires | Avis sur les biens |
| Recommandations | Partage de bien entre users |
| Notifications | Notifications in-app |
| Messages | Messagerie interne |
| Disponibilites | Calendrier des biens |
| BienLikes | Likes sur les biens |
| BienVues | Vues uniques par bien |
| Payments | Paiements MTN MoMo |
| NewsletterSubscribers | Abonnés newsletter |
| CommissionTransactions | Transactions avec calcul taxe/commission |

## Rôles utilisateurs

| Rôle | Accès |
|---|---|
| Client | Navigation, réservations, favoris |
| Proprietaire | Publie des biens, gère ses annonces |
| UserIndep | Idem Proprietaire |
| AdminRegion | Modère les biens, valide les comptes pro |
| AdminPays | Accès étendu toutes régions |
| DirecteurProjet | Accès total |

## Types de comptes professionnels (TypeCompteProf)
- `Proprietaire` — propriétaire particulier
- `PromoteurImmobilier` — promoteur
- `AgenceImmobiliere` — agence (peut vendre terrains directement)
- `AgentImmobilier` — agent (1 an gratuit puis abonnement annuel)

**Why:** Chaque type a des règles métier différentes pour les commissions et le routage des transactions.
**How to apply:** Toujours vérifier `TypeCompteProf` avant d'appliquer une règle de commission ou de routage.
