---
name: project-business-rules
description: "Règles métier du projet Levetimmo — commissions, abonnements, routage transactions, modération"
metadata: 
  node_type: memory
  type: project
  originSessionId: 40b1ed6f-e44c-47c0-b6dc-fc1979ad94a7
  modified: 2026-08-31T08:32:50.579Z
---

# Règles métier — Levetimmo

## 1. Qui peut vendre/louer directement (sans passer par Levetimmo)

| Type de compte | Vente terrain | Location < 50k XAF | Location ≥ 50k XAF | Vente bien |
|---|---|---|---|---|
| AgenceImmobiliere | ✅ Direct | ✅ Direct | Levetimmo | Levetimmo |
| AgentImmobilier | Levetimmo | ✅ Direct | Levetimmo | Levetimmo |
| Proprietaire | Levetimmo | Levetimmo | Levetimmo | Levetimmo |
| PromoteurImmobilier | Levetimmo | Levetimmo | Levetimmo | Levetimmo |

## 2. Commissions sur les transactions gérées par Levetimmo

- **Calcul :** Montant brut → déduction taxes → net → split 50/50
- **Taxe vente :** 10% (droits d'enregistrement Cameroun)
- **Taxe location :** 15% (TVA + impôts fonciers)
- **Part Levetimmo :** 50% du net après impôts
- **Part agent/propriétaire :** 50% du net après impôts
- **Levetimmo régule les impôts** (se charge de la déclaration fiscale)

Exemple : bien vendu 5 000 000 XAF
→ Taxe 10% = 500 000 XAF
→ Net = 4 500 000 XAF
→ Levetimmo = 2 250 000 XAF | Agent = 2 250 000 XAF

## 3. Abonnement AgentImmobilier

- **Période gratuite :** 1 an à partir de `DateDevenirPro` (date de validation du compte)
- **Après 1 an :** abonnement annuel requis (montant à définir par le boss)
- **Blocage :** si abonnement expiré, le endpoint `POST /api/transactions` renvoie `doitPayer: true`
- **Activation/renouvellement :** admin via `POST /api/transactions/abonnement-payer/{userId}`

## 4. Modération des biens

- **Tout bien publié** par un non-admin → statut "En attente"
- **Tous les admins** (AdminRegion, AdminPays, DirecteurProjet) sont notifiés → **sans filtre de région**
- **Un seul admin actuellement** (AdminRegion Littoral) gère toutes les régions
- **Valider** → `StatutPublication = "Valide"`, `EstDisponible = true`, notification propriétaire
- **Rejeter** → `StatutPublication = "Rejetée"`, notification avec motif, propriétaire peut resoumettre

⚠️ Ne jamais ajouter de filtre `u.Region == ...` dans la logique admin sans demande explicite.

## 5. Frais de visite

- Configurés par le publieur : 0 (gratuit), 2 000, 3 000 ou 5 000 XAF
- Si frais > 0 → paiement MTN MoMo requis avant confirmation de visite

## 6. Validation de date de réservation

- La date de visite doit être ≥ aujourd'hui
- Bloqué côté frontend : attribut `min` dynamique + check JS au submit
