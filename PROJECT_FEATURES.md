---
name: project-features-done
description: "Toutes les fonctionnalités déjà implémentées sur Levetimmo, avec leur état en production"
metadata: 
  node_type: memory
  type: project
  originSessionId: 40b1ed6f-e44c-47c0-b6dc-fc1979ad94a7
  modified: 2026-08-31T08:32:04.298Z
---

# Fonctionnalités implémentées — Levetimmo

Dernière mise à jour : 2026-08-31. Tout ce qui suit est en production sur Railway (levetimmo.com).

## Authentification & Sécurité
- ✅ Inscription / Connexion email + mot de passe (JWT)
- ✅ Connexion Google OAuth (GSI — Google Sign-In côté frontend, endpoint `/api/auth/google`)
- ✅ Authentification 2 facteurs (2FA) par OTP email
- ✅ Réinitialisation de mot de passe par email (token temporaire)
- ✅ Blocage de compte par admin (`EstBloque`)
- ✅ KYC — vérification d'identité (CNI/Passeport + selfie), statuts : NonSoumis / EnAttente / Approuve / Rejete

## Gestion des biens immobiliers
- ✅ Publication d'une annonce (titre, type, statut vente/location, prix, surface, chambres, etc.)
- ✅ Upload image principale + galerie (multi-photos)
- ✅ Coordonnées GPS (latitude/longitude) via sélecteur de carte
- ✅ Titre foncier (PDF/image) uploadé à la publication
- ✅ Documents spécifiques terrain (certificat propriété, statut civil, régime matrimonial)
- ✅ Documents spécifiques immeuble (permis de bâtir, plan bâtiment)
- ✅ Frais de visite configurables par le publieur (0 / 2000 / 3000 / 5000 XAF)
- ✅ Modération admin : statuts En attente / Validée / Rejetée
- ✅ Checklist documents vérifiés par admin (JSON)
- ✅ Compteur de vues unique par utilisateur (`BienVues`)
- ✅ Likes sur les biens (`BienLikes`)
- ✅ Biens similaires sur la page détail

## Modération (Espace Admin)
- ✅ Tableau de bord admin (`espace_admin_region.html`)
- ✅ Liste tous les biens en attente / validés / rejetés (sans filtre région — un admin gère tout)
- ✅ Valider un bien → mis en ligne + notification propriétaire
- ✅ Rejeter un bien avec motif → notification propriétaire
- ✅ Validation des demandes de compte professionnel
- ✅ Gestion des utilisateurs (bloquer/débloquer)
- ✅ Validation KYC

## Comptes professionnels
- ✅ Demande de passage au statut professionnel (formulaire + pièce d'identité + selfie)
- ✅ Types : Proprietaire, PromoteurImmobilier, AgenceImmobiliere, AgentImmobilier
- ✅ NIU (Numéro d'Identification Unique fiscal)
- ✅ Nom d'agence
- ✅ `DateDevenirPro` automatiquement renseigné à la validation pour AgentImmobilier

## Réservations & Visites
- ✅ Formulaire de demande de visite (prénom, nom, email, téléphone, lieu, date, message)
- ✅ Validation de date : impossible de choisir une date passée (min=today + check JS)
- ✅ Suivi des réservations dans l'espace utilisateur
- ✅ Paiement des frais de visite via MTN MoMo (si frais > 0)

## Paiements
- ✅ MTN Mobile Money via CamPay (Collection API)
- ✅ Statuts : EnAttente / Reussi / Echoue / Expire
- ✅ Table `Payments` avec référence, montant, numéro payeur, lien réservation/bien

## Commissions & Taxes (ajouté 2026-08-29)
- ✅ Service `TaxCommissionService` : calculateur de taxe (10% vente / 15% location)
- ✅ Règles de routage :
  - AgenceImmobiliere vendant un terrain → direct (pas de commission Levetimmo)
  - Location < 50 000 XAF → agent gère directement
  - Tout le reste → Levetimmo, commission 50/50 après impôts
- ✅ Abonnement AgentImmobilier : 1 an gratuit, puis abonnement annuel
- ✅ Endpoint `GET /api/transactions/calculer-taxe` (calculateur public)
- ✅ Table `CommissionTransactions` en base

## Espace utilisateur connecté
- ✅ Tableau de bord (`accueil_user.html`)
- ✅ Mes réservations (liste + statut)
- ✅ Mes favoris
- ✅ Messagerie interne (conversations par bien)
- ✅ Notifications in-app (badge + liste)
- ✅ Profil utilisateur (modifier nom, prénom, téléphone, photo)

## Espace propriétaire
- ✅ Liste de ses biens avec statut de publication
- ✅ Modifier / supprimer un bien
- ✅ Resoumission d'un bien rejeté
- ✅ Calendrier des disponibilités (bloquer des dates)
- ✅ Recommander un bien à un autre utilisateur

## Fonctionnalités sociales & engagement
- ✅ Avis et commentaires sur les biens (note 1-5 + texte)
- ✅ Likes sur les biens
- ✅ Recommandations de biens entre utilisateurs
- ✅ Newsletter (inscription, liste admin, envoi groupé)
- ✅ Formulaire de contact (`/api/contact`)

## Notifications
- ✅ Notifications in-app pour : nouvelle réservation, bien validé/rejeté, demande pro validée/refusée, nouveau message, nouveau bien à modérer
- ✅ Tous les admins (AdminRegion, AdminPays, DirecteurProjet) notifiés à chaque publication — sans filtre de région

## SEO & Performance
- ✅ Page loader (spinner pendant le chargement)
- ✅ Service Worker (`sw.js`)
- ✅ Images servies depuis `/uploads`
- ✅ Carte interactive MapLibre sur page détail
