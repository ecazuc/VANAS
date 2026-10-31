# Données personnelles : ce que VANAS conserve, pourquoi, combien de temps

Document de travail pour le café (RGPD). À faire relire par un juriste avant l'ouverture, en particulier le point sur les mineurs. Les durées sont réglables dans `VANAS.Server/appsettings.json`.

## Données conservées

| Donnée | Pourquoi | Durée |
|---|---|---|
| Identifiant, mot de passe (haché, jamais en clair), pseudo, avatar | Compte joueur | Tant que le compte existe ; supprimé avec le compte |
| Date de naissance | Vérifier l'âge minimum (12 ans) et l'accord parental | Tant que le compte existe |
| Date et auteur de l'accord parental | Preuve que le parent a donné son accord | Tant que le compte existe |
| Sessions de jeu (poste, début, fin, durée) | Facturation du temps, historique du joueur | Tant que le compte existe |
| Ventes (formule, prix, moyen de paiement, qui a encaissé) | Comptabilité | 10 ans (obligation comptable) ; le nom du joueur y est copié |
| Inscriptions aux événements et billets | Organisation des événements | Tant que le compte existe |
| Messages de la messagerie | Fonctionnement de la messagerie et modération | **90 jours** (`Chat:RetentionDays`) |
| Messages signalés et signalements | Preuve en cas de litige ou de signalement abusif | **12 mois après le traitement** du signalement (`Chat:ReportRetentionDays`) ; tant qu'un signalement est ouvert |
| Sanctions de messagerie (silence, bannissement) | Faire respecter les décisions | Tant que la sanction est active |
| Jeux auxquels le joueur dit jouer, blocages | Salons et suggestions | Tant que le compte existe |

La suppression d'un compte (console staff) supprime ses messages, blocages, signalements, jeux et sanctions de messagerie. Les ventes passées sont conservées pour la comptabilité.

## Règle d'âge

- Moins de 12 ans : inscription refusée.
- De 12 à 17 ans : le compte est créé, mais le joueur ne peut ni jouer, ni utiliser la messagerie, ni s'inscrire à un événement tant qu'un membre du staff n'a pas enregistré l'accord d'un parent ou responsable légal **au comptoir**. Le staff peut retirer cet accord ; le joueur est alors déconnecté.
- Seuil réglable : `Age:ParentalConsentBelow` (18 par défaut ; la loi française fixe à 15 ans l'âge du consentement numérique seul).
- Les comptes sans date de naissance (compte administrateur, comptes de test) sont traités comme des adultes.

## Messagerie : protections

Règles à accepter avant tout usage ; messages privés seulement entre joueurs qui partagent un jeu ; blocage ; signalement ; liens, numéros de téléphone et messages répétitifs refusés ; 6 messages maximum par 10 secondes ; liste de mots masqués configurable (`Chat:BannedWords`) ; masquage, silence (24 h, 7 jours) et bannissement par le staff.

**Choix assumé du café :** les mineurs et les adultes peuvent s'écrire en privé. Cela relève de la responsabilité du café ; la modération et le signalement sont les garde-fous.

## À faire avant l'ouverture

- Mentions légales et politique de confidentialité sur le site (W-5).
- Formulaire papier d'accord parental à faire signer au comptoir (identité du parent, date).
- Droit d'accès et de suppression : aujourd'hui la suppression d'un compte passe par le staff ; un bouton dans « Mon compte » viendra avec W-5.
- Sauvegardes de la base : durée de conservation à aligner sur les durées ci-dessus.
