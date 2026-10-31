# VANAS — Plan de développement (cybercafé)

## Contexte

Lancement d'un cybercafé. Il faut 3 produits + un backend commun, tous en .NET :
1. **App PC (kiosque)** qui remplace l'écran de login Windows : connexion client, temps restant, profil, lancement/installation de jeux (Steam / Epic / Riot…).
2. **Site web** : présentation de la structure, blog actus jeux vidéo, événements.
3. **App mobile** : temps restant, inscription aux événements + QR code d'entrée, messagerie entre joueurs partageant les mêmes jeux.

**Avis sur .NET : bon choix.** Un seul langage (C#), un projet `Shared` pour les modèles/DTO réutilisés partout, une seule API. Décisions validées : **Blazor Web App** (site), **.NET MAUI** (mobile), **launchers lancés via leurs protocoles, comptes clients** (pas de comptes partagés).

## Dépôt existant (https://github.com/ecazuc/VANAS) — état des lieux

`VANAS.sln` avec 3 projets (net8.0) :
- `VANAS.Server` : ASP.NET Core Web API + EF Core SQLite + BCrypt. `AuthController` (login, register, add-time, update-balance, kick, set-admin, delete, users).
- `VANAS.Client` : WPF — `AuthWindow`, `RegisterWindow`, `Dashboard` (timer local, liste de jeux codée en dur, `Process.Start`), `AdminPanel`. Appels API via Refit (`IGamingApi`).
- `VANAS.Shared` : `User`, `UserSession`, `GameItem`.

**Problèmes à corriger avant d'empiler des fonctionnalités (Étape 0) :**
- Aucun endpoint n'est protégé : n'importe qui peut appeler `add-time`, `users` (renvoie les hash), `set-admin`, `delete`. `UserSession` mentionne un JWT mais il n'est pas implémenté.
- Le compte admin seed a le mot de passe en clair `admin123` alors que le login vérifie avec BCrypt → l'admin ne peut pas se connecter.
- `vanas.db` / `-shm` / `-wal` sont commités ; `EnsureCreated()` utilisé en parallèle des migrations EF (incompatible).
- Le décompte du temps est fait côté client (falsifiable, perdu si le PC plante) ; le « kick » est un `BalanceMinutes = -1` bricolé.
- Chemins launchers en dur (`Q:\Riot Games\...`), fenêtre non verrouillée (pas de vrai kiosque), `User` mélange compte/joueur.
- Dossier local `Q:\projects\VANNAS` vide → cloner le dépôt dans ce dossier.

## Architecture cible

```
VANAS.sln
├─ VANAS.Shared     DTO, enums, contrats (référencé par tous)
├─ VANAS.Server     API REST + SignalR (temps réel) + EF Core  → SQLite en dev, PostgreSQL en prod
├─ VANAS.Client     WPF kiosque PC clients uniquement (aucun code admin)
├─ VANAS.Web        Blazor Web App (site public + console staff /admin : postes, temps, comptes, blog, événements)
├─ VANAS.Mobile     .NET MAUI (Android d'abord, iOS ensuite)
└─ VANAS.Tests      xUnit (API, logique de temps)
```

Principes : un seul serveur = source de vérité (temps, comptes, événements). JWT + refresh token ; rôles `Client`, `Staff`, `Admin`. SignalR pour : temps restant live, kick/ajout de temps, messagerie. Hébergement : serveur dans le café (LAN) pour le PC, + exposition internet (VPS ou reverse proxy) pour site/mobile — à trancher à l'étape S3.

## Direction design (commune aux 3 apps)

Style **cyberpunk jaune / gris** : fond gris anthracite (`#16181C` → `#23262D`), accent jaune néon (`#FCEE0A`), jaune atténué pour les survols (`#C9BE00`), texte gris clair (`#C8CCD4`), alertes rouge/cyan en touches rares. Coins coupés (chanfreins), bordures fines jaunes avec légère lueur, typographie technique (Rajdhani / Orbitron pour titres, Inter pour le texte), effets discrets (scanlines, glitch au survol). Les couleurs sont définies une seule fois (`ResourceDictionary` WPF, variables CSS pour le web, `Colors.xaml` MAUI) pour rester cohérentes. Un kit de composants (bouton, carte, champ, barre de temps) est livré à l'étape PC-1 puis décliné sur le web (W-1) et le mobile (M-1).

## Décision : cloner et corriger

On clone le dépôt existant et on corrige (Étape 0) plutôt que de recommencer : le socle est sain, seuls la sécurité et quelques bricolages sont à refaire.

## Règle de travail : un jalon = une livraison testable

Chaque étape ci-dessous se termine par **« À tester »** (checklist que vous exécutez) puis un **point de correction** : vous testez, listez les retours, je corrige avant de passer à l'étape suivante. Une branche + un tag git par étape (`etape-0`, `pc-1`…), pour pouvoir revenir en arrière.

---

## ÉTAPE 0 — Fondations (backend + sécurité) — prérequis de tout

| # | Tâche |
|---|---|
| 0.0 | Retirer `AdminPanel` de `VANAS.Client` (code admin hors des PC clients) ; il sert de référence pour la console staff web (PC-6). |
| 0.1 | Cloner le dépôt dans `Q:\projects\VANNAS`, retirer `vanas.db*` du suivi git (`.gitignore`), ajouter le vrai `README` + ce plan en `PLAN.md`. |
| 0.2 | Passer sur les migrations EF uniquement (retirer `EnsureCreated`), seed admin avec mot de passe hashé lu depuis la config/variable d'environnement. |
| 0.3 | JWT + rôles ; protéger tous les endpoints (`[Authorize]`, `Admin`/`Staff` pour add-time/kick/users…) ; ne plus jamais renvoyer `PasswordHash`. |
| 0.4 | Séparer `User` (compte) / `Profile` (pseudo, avatar, jeux favoris) ; modèle `TimeLedger` (journal des ajouts/consommations) au lieu d'un simple solde modifiable. |
| 0.5 | Sessions de jeu côté serveur : `StartSession` / heartbeat toutes les 30 s / `EndSession` ; le serveur décompte le temps ; kick propre (événement, pas `-1`). |
| 0.6 | Tests xUnit sur login, droits, décompte du temps. |

**À tester :** Swagger — login admin OK ; appel sans token = 401 ; client simple ne peut pas `add-time` ; couper le client en plein décompte → le solde reste cohérent.

---

## PARTIE A — Application PC (WPF kiosque)

| Étape | Contenu | À tester |
|---|---|---|
| **PC-1** Login & session | Brancher le client sur JWT/sessions serveur ; écran de login plein écran stylé ; compte à rebours alimenté par le serveur ; gestion perte réseau (mode dégradé + reconnexion) ; fin de temps = verrouillage. | Login, voir le temps passer, ajouter du temps depuis le panel admin → le PC se met à jour sans relancer ; débrancher le réseau 1 min. |
| **PC-2** Mode kiosque (remplace le login Windows) | Verrouillage : plein écran topmost, blocage Alt+Tab/Win/Ctrl+Alt+Suppr côté app + stratégie Windows (Shell Launcher / « Assigned Access » ou remplacement du shell `explorer.exe` par VANAS pour un compte `Client`), compte Windows standard sans droits admin, sortie secrète admin (code PIN, remplace le `Shift+F12` actuel). Démarrage auto. | Redémarrer le PC → arrive directement sur VANAS ; impossible d'ouvrir le gestionnaire de tâches / explorateur ; sortie admin par PIN. **Tester d'abord sur une VM**. |
| **PC-3** Profil & temps | Page profil (pseudo, avatar, historique des sessions, temps total joué), affichage du temps restant permanent (overlay discret en jeu), alertes à 10 / 5 / 1 min. | Modifier son profil ; overlay visible au-dessus d'un jeu ; alerte à 5 min. |
| **PC-4** Bibliothèque & lancement de jeux | Détection auto des launchers installés (Steam, Epic, Riot, Battle.net) et des jeux installés (lecture manifests Steam `appmanifest_*.acf` / Epic `.item`) ; catalogue de jeux géré par le serveur (staff) ; lancement via protocoles (`steam://rungameid/…`). Remplace la liste en dur de `Dashboard.xaml.cs`. | Les jeux réellement installés apparaissent avec leur image ; un clic lance le jeu ; fermeture du jeu à la fin du temps. |
| **PC-5** Installation de jeux | Bouton « Installer » : `steam://install/<appid>`, `com.epicgames.launcher://apps/<id>?action=install`, Riot via `RiotClientServices.exe --launch-product=…` ; suivi de statut ; quotas/espace disque ; liste blanche des jeux autorisés par le staff. **Contrainte : le client se connecte à son propre compte launcher** (mot de passe jamais stocké par VANAS) ; nettoyage des sessions launcher à la déconnexion VANAS. | Installer un petit jeu gratuit depuis Steam puis Epic ; déconnexion → le compte Steam du client est bien fermé. |
| **PC-6** Console staff (web, `/admin` dans `VANAS.Web`, rôle Staff/Admin) | Remplace l'`AdminPanel` WPF : liste des postes en direct (SignalR), ajout/retrait de temps, kick, message à un poste, création de comptes, vente de forfaits, rapports de caisse simples. Nécessite le squelette `VANAS.Web` (W-1) en amont. | Depuis le navigateur du comptoir, voir 2 PC clients, ajouter du temps, kicker. |
| **PC-7** Packaging | Installateur (MSIX/Inno Setup), config du serveur par fichier, mise à jour automatique, logs. | Installation propre sur un PC vierge. |

---

## PARTIE B — Site web (Blazor Web App)

| Étape | Contenu | À tester |
|---|---|---|
| **W-1** Squelette & vitrine | Projet `VANAS.Web`, charte graphique, pages Accueil, Le café (tarifs/forfaits, photos, config PC), Contact/Horaires/Plan d'accès, SEO de base, responsive. | Parcourir sur desktop + téléphone ; Lighthouse > 90. |
| **W-2** Blog actus | Modèle `Article` (titre, slug, contenu Markdown, image, catégorie, tags, date) ; liste paginée, page article, filtres par catégorie/jeu ; **espace staff** (login rôle Staff) pour rédiger/publier ; flux RSS. | Créer un article en staff → visible sur le site ; filtre par jeu. |
| **W-3** Événements | Modèle `Event` (date, jeu, places, tarif) ; liste/calendrier, page détail, places restantes ; création par le staff ; **inscription depuis le site** (compte client) — même API que le mobile. | Créer un tournoi, s'inscrire, voir les places baisser ; staff voit la liste des inscrits. |
| **W-4** Comptes clients web | Connexion/inscription avec le même compte que le PC, page « mon compte » (temps restant, historique, mes événements). | Un compte créé sur le PC se connecte sur le site, et inversement. |
| **W-5** Déploiement | Hébergement (VPS + HTTPS Let's Encrypt, nom de domaine), CI GitHub Actions, sauvegardes BDD, RGPD (mentions légales, cookies, suppression de compte). | Site accessible en HTTPS public ; restauration d'une sauvegarde. |

---

## PARTIE C — Application mobile (.NET MAUI)

| Étape | Contenu | À tester |
|---|---|---|
| **M-1** Socle & connexion | Projet `VANAS.Mobile` (Android en premier), navigation Shell, login (même compte), stockage sécurisé du token (`SecureStorage`). | App installée sur un vrai téléphone Android ; login/déconnexion. |
| **M-2** Temps restant | Écran d'accueil : solde, session en cours en direct (SignalR), historique, notification « il vous reste 10 min ». | Lancer une session sur un PC → le mobile affiche le décompte en direct ; push à 10 min. |
| **M-3** Événements & QR code | Liste des événements, inscription/désinscription, **QR code de billet** (JWT signé court, généré côté serveur ; `ZXing.Net.Maui` pour l'affichage). Écran staff « Scanner » (caméra) qui valide l'entrée et refuse les doublons. | S'inscrire à un événement, afficher le QR, le scanner avec un 2ᵉ téléphone → « valide » ; 2ᵉ scan → « déjà utilisé ». |
| **M-4** Messagerie | Table `PlayerGame` (jeux joués par chaque joueur) ; suggestions de joueurs partageant des jeux ; salons par jeu + messages privés ; SignalR ; notifications push ; **modération** (signalement, blocage, mute, filtre, mineurs). | 2 comptes avec le même jeu se trouvent et s'écrivent en temps réel ; signalement d'un message visible côté staff. |
| **M-5** Publication | Icône/splash, Firebase pour push, build signé, Google Play (test interne), puis iOS (compte développeur Apple, TestFlight) si souhaité. | Installation via lien de test interne. |

---

## Ordre de livraison recommandé

`Étape 0` → `PC-1` → `PC-2` (c'est le cœur du service, à valider sur VM puis sur un vrai poste) → `PC-3/4/5` → `W-1` (squelette) → `PC-6` (console staff) → `W-2/W-3` → `M-1/M-2/M-3` → `M-4` → `PC-7`, `W-5`, `M-5` à l'approche de l'ouverture.

Le MVP d'ouverture = Étape 0 + PC-1 à PC-4 + PC-6 + W-1. Le reste peut sortir après l'ouverture.

## Points de vigilance (à décider en cours de route)

- **Paiement/forfaits** : encaissement en caisse (staff ajoute du temps) au départ ; paiement en ligne (Stripe) plus tard.
- **Licences/CGU** : Steam/Epic/Riot ont des conditions spécifiques pour les cybercafés (ex. Steam « Cafe Program », Riot « Internet Cafe Program ») — à vérifier avant l'ouverture.
- **Données perso/RGPD** et messagerie entre joueurs (mineurs, modération) — conception dès M-4.
- **Sécurité des postes** : ne jamais compter uniquement sur l'app WPF ; durcissement Windows (comptes limités, GPO) indispensable.

## Vérification (comment on valide chaque étape)

- Étape 0 : `dotnet build` + `dotnet test` verts, scénarios Swagger ci-dessus.
- PC : lancer `VANAS.Server` + `VANAS.Client` (F5 multi-projets via `VANAS.slnLaunch.user`), puis test sur VM Windows.
- Web : `dotnet run` puis navigation manuelle + Lighthouse.
- Mobile : déploiement sur un téléphone Android réel (caméra/push non testables en émulateur).
- Chaque étape : checklist « À tester » → retours → corrections → tag git → étape suivante.

## Première action à l'approbation du plan

Cloner le dépôt dans `Q:\projects\VANNAS`, y copier ce document en `PLAN.md`, puis attaquer l'**Étape 0** (note : le SDK .NET n'est pas détecté dans le shell actuel — à installer/vérifier : .NET 8 SDK minimum, + workload `maui` et Visual Studio 2022 pour les parties mobile/WPF).

---

## Décisions complémentaires (contexte café)

- **Échelle** : 40 postes au lancement, extensible (dimensionnement prévu jusqu'à ~200). Table `Station` (nom, IP, statut) dès l'Étape 0.
- **Serveur** : PC dédié dans le café (Ethernet, onduleur, sauvegardes auto, mini-PC de secours) pour login/temps/jeux en LAN. **PostgreSQL** à la place de SQLite dès l'Étape 0.
- **Hybride** : petit VPS externe pour site public, blog, événements, mobile, messagerie ; synchronisation par connexion sortante depuis le café (aucune entrée internet sur le serveur local). À mettre en place à W-5 / M-2.
- **Tarification** : paiement à l'heure (crédits d'heures) **ou** forfait mensuel. `TimeLedger` + `Offer` (type, durée/heures incluses, prix) + `Subscription` (début/fin). Tarifs non figés : données modifiables par le staff.
- **Nom & logo** : provisoires. Centralisés (ressources/config) pour remplacement final sans toucher au code.

---

## Avancement (mis à jour au fil des étapes)

| Étape | État | Branche |
|---|---|---|
| Étape 0 — fondations serveur/sécurité | Validée, poussée | `etape-0` |
| PC-1 — client login/session | Validée, poussée | `etape-0` |
| W-1 — site vitrine + PC-6 console staff | Validées, poussées | `etape-0` |
| PC-2 — mode kiosque | Code terminé, **en attente de test en VM** (VirtualBox, `tools/Create-TestVM.ps1`) | `pc-2` |
| PC-3 — profil, overlay en jeu, alertes 10/5/1 min | Code terminé, à tester | `pc-3` |
| PC-4 — bibliothèque et lancement de jeux (catalogue serveur + console /admin/jeux, détection Steam/Epic/Riot) | Code terminé, à tester | `pc-4` |
| PC-5 — installation de jeux (confirmation, progression Steam, espace disque, nettoyage des comptes launchers) | Code terminé, à tester | `pc-5` |
| W-2 — blog d'actus (Markdown sécurisé, éditeur staff avec aperçu, RSS) | Code terminé, à tester | `w-2` |
| W-3 — événements (API, inscription + billet à code, contrôle d'entrée, pages publiques et staff) | Code terminé, à tester | `w-3` |
| W-4 — comptes clients sur le site (connexion, création de compte, Mon compte, inscription aux événements depuis la page de l'événement) | Code terminé, à tester | `w-4` |
| M-1 — application mobile : socle MAUI, connexion/création de compte, jeton sécurisé, accueil avec le temps restant (testé en version Windows ; Android à tester sur téléphone) | Code terminé, à tester | `m-1` |
| M-2 — temps restant en direct sur le mobile (décompte, EN JEU/HORS SESSION, mise à jour immédiate via SignalR, alertes 10/5/1 min dans l'app, historique) ; notification poussée app fermée = M-5 (Firebase) | Code terminé, à tester | `m-2` |
| M-3 — événements sur mobile, inscription, billet avec QR code, scanner staff (caméra Android + saisie manuelle) ; code caméra Android non compilé faute d'Android SDK | Code terminé, à tester | `m-3` |
| Tarifs — formules (heures, forfait illimité avec plafond par jour, forfait d'heures), vente au comptoir, caisse du jour, grille publique ; heures sans expiration | Code terminé, à tester | `tarifs` |
| M-4 - messagerie : jeux joués, salons par jeu, messages privés, blocage, signalement, modération staff (masquer, silence, bannissement), filtre liens/téléphones/spam, limite de vitesse, temps réel | Code terminé, à tester | `m-4` |
| W-5 / M-5 / PC-7 | À faire | |
