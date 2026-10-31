# Batterie de tests manuels

Cochez au fur et à mesure et notez le moindre défaut (message exact, capture si possible).

## Préparation
1. Lancer le serveur : `dotnet run --project VANAS.Server --launch-profile http` (http://localhost:5049/swagger).
2. Lancer le site : `dotnet run --project VANAS.Web --launch-profile http` (http://localhost:5100).
3. Lancer le client : `dotnet run --project VANAS.Client` (`Shift+F12` pour quitter hors kiosque).

Comptes de test (base de développement) :

| Compte | Mot de passe | Rôle | Remarque |
|---|---|---|---|
| `admin` | `Admin-Dev-123!` | Admin | console staff |
| `joueur1` | `motdepassetest` | Client | beaucoup de temps, pseudo « Joueur Un » |
| `alerteur` | `motdepassetest` | Client | environ 9 min : pour voir les alertes |
| `testeur` | `motdepassetest` | Client | (mot de passe à rétablir si besoin) |
| `staffeur` | `motdepassetest` | Staff | pour le scanner de l'application mobile |

Pour recharger un compte : console staff, onglet Comptes, « + min ».

## A. Client PC : connexion et session (PC-1)
- [ ] Mauvais mot de passe : message rouge explicite (pas de blocage).
- [ ] Inscription avec mot de passe < 8 caractères : message « Password must be at least 8 characters » (ou équivalent).
- [ ] Inscription valide puis connexion : refus « Plus de temps » tant que le compte n'a pas de minutes.
- [ ] Connexion `joueur1` : le décompte démarre, l'indicateur reste « EN LIGNE ».
- [ ] Ajouter du temps depuis la console staff : le décompte remonte en moins de 2 secondes.
- [ ] Kicker depuis la console : retour au login avec « Session fermée par le staff » en moins de 2 secondes.
- [ ] Couper le serveur pendant une session : « HORS LIGNE », le décompte continue ; au bout de 2 minutes la session se verrouille.

## B. Profil, overlay, alertes (PC-3)
- [ ] Onglet PROFIL : pseudo, avatar, temps joué, sessions, historique cohérents.
- [ ] Changer pseudo et avatar : enregistré, visible en haut à gauche.
- [ ] Pseudo déjà pris (même en changeant la casse) : refusé.
- [ ] Compte `alerteur` : bannière rouge « Il vous reste 10 minutes » (puis 5, puis 1).
- [ ] Ouvrir une autre application : l'horloge flottante apparaît en haut de l'écran, sans prendre le focus.

## C. Jeux (PC-4 et PC-5)
- [ ] Onglet JEUX : les jeux réellement installés sur le PC sont marqués « JOUER », les autres « INSTALLER ».
- [ ] Clic sur un jeu installé : le jeu (ou son launcher) démarre.
- [ ] Recherche : filtre en direct.
- [ ] Console staff, page Jeux : masquer un jeu, il disparaît du client sous 20 s ; le réactiver, il revient.
- [ ] Console staff : ajouter un jeu avec un identifiant piégé (`x & calc.exe`) : refusé.
- [ ] Clic sur un jeu non installé : demande de confirmation ; second clic : le launcher ouvre sa fenêtre d'installation.
- [ ] Pendant l'installation : la tuile indique la progression (Steam) ou « en cours », et un autre jeu ne peut pas être installé en même temps.
- [ ] Une fois installé, la tuile passe seule en « JOUER ».

## D. Mode kiosque (PC-2), dans la VM uniquement
Voir `kiosk/README.md`. Ne PAS tester sur votre session de travail.
- [ ] Après installation et redémarrage : arrivée directe sur l'écran VANAS, sans bureau.
- [ ] Touche Windows, Alt+Tab, Ctrl+Échap, Alt+F4 inactifs sur l'écran de login.
- [ ] Gestionnaire de tâches (Ctrl+Maj+Échap) inaccessible.
- [ ] En session : Alt+Tab libre, touche Windows bloquée.
- [ ] Déconnexion : les programmes lancés pendant la session sont fermés.
- [ ] Déconnexion : Steam / Epic / Riot ne sont plus connectés au compte précédent.
- [ ] `Ctrl+Maj+F12` puis le PIN : sortie. Trois PIN faux : blocage 1 minute.

## E. Site web (W-1, W-2) et console staff (PC-6)
- [ ] Accueil et « Le café » s'affichent correctement, sur ordinateur et sur téléphone (réduire la fenêtre).
- [ ] `/admin` sans connexion : redirection vers la connexion staff ; un compte client est refusé.
- [ ] Postes en direct : un client connecté apparaît « EN JEU » sans recharger ; le bouton Kicker fonctionne.
- [ ] Comptes : création avec minutes offertes, ajout de temps, kick, recherche.
- [ ] Comptes (rôle Admin) : changement de rôle et suppression ; un compte Staff ne les voit pas.
- [ ] Blog : créer un brouillon (invisible sur `/blog`), le publier (visible), modifier le titre (l'adresse ne change pas).
- [ ] Blog : coller `<script>alert(1)</script>` et `[x](javascript:alert(1))` dans le contenu : aucun script ne s'exécute sur la page publique.
- [ ] `/rss.xml` liste les articles publiés.

## F. Événements (W-3)
Un événement de démonstration (« Tournoi Rocket League ») existe dans la base de développement.
- [ ] Console staff, page Événements : créer un événement (brouillon) : absent de `/evenements` ; le publier : il apparaît.
- [ ] `/evenements` et la page de détail : date, jeu, tarif, places restantes, description Markdown.
- [ ] Swagger, connecté en `joueur1` : `POST /api/events/{id}/register` renvoie un billet (code de 10 caractères) ; une 2e fois : « déjà inscrit ».
- [ ] La page publique et la console affichent une place de moins ; le bouton « Inscrits » liste le joueur et son billet.
- [ ] Événement à 1 place : un 2e joueur est refusé (« Complet »).
- [ ] Console, « Contrôle d'entrée » : saisir le code du billet valide l'entrée (nom du joueur affiché) ; une 2e saisie : « Billet déjà utilisé ».
- [ ] Réduire la capacité sous le nombre d'inscrits : refusé.
- [ ] Supprimer l'événement : disparaît du site et de la console.

## G. Comptes clients sur le site (W-4)
- [ ] « Mon compte » (menu du site) sans être connecté : redirection vers la connexion.
- [ ] Créer un compte depuis le site (mot de passe < 8 caractères : message en français ; identifiant déjà pris : idem) puis se connecter avec ce même compte sur le client PC.
- [ ] Connexion avec un mauvais mot de passe : « Identifiant ou mot de passe incorrect. »
- [ ] Connexion `joueur1` : page Mon compte avec pseudo, temps restant, temps joué, billets, dernières sessions.
- [ ] Sur la page d'un événement, non connecté : bouton « Se connecter ou créer un compte », qui ramène sur l'événement après connexion.
- [ ] Connecté : « Je m'inscris » affiche le billet (code) ; le billet apparaît dans Mon compte ; « Me désinscrire » le retire.
- [ ] Événement complet : message « complet », pas de bouton.
- [ ] Après validation du billet à l'entrée (console staff), plus de bouton de désinscription.
- [ ] Déconnexion depuis Mon compte, puis retour sur la page d'un événement : le bouton de connexion revient.

## H. Application mobile (M-1 à M-3)
Le serveur doit tourner. Version PC : voir `VANAS.Mobile/README.md`. Sur téléphone Android, indiquez d'abord l'adresse du serveur (Paramètres du serveur).
- [ ] Connexion avec `alerteur` ; fermer et rouvrir l'application : elle reste connectée.
- [ ] Mauvais mot de passe : message en français ; création d'un compte depuis le mobile, puis connexion avec ce compte sur le client PC.
- [ ] Accueil : sur un PC du café, connectez-vous : le téléphone passe à « EN JEU » avec le nom du poste en moins de 15 s, et le décompte tourne.
- [ ] Console staff : ajoutez 5 min au joueur en jeu : le téléphone est à jour en moins de 3 s ; kickez-le : « HORS SESSION » dès que le PC se verrouille.
- [ ] Avec peu de temps (compte `alerteur`) : bandeau rouge à 10, 5 et 1 minute.
- [ ] Événements : liste, détail (description, places, tarif), « Je m'inscris » : billet avec QR code et code à 10 caractères ; « Me désinscrire » le retire ; événement complet : pas de bouton.
- [ ] Un joueur n'a pas l'onglet Scanner ; `staffeur` l'a.
- [ ] Scanner (Android, caméra) : autoriser la caméra, scanner le QR code d'un autre téléphone : « ENTRÉE VALIDÉE » avec le nom du joueur ; un second scan : « déjà utilisé ». Saisie manuelle du code : même résultat.
- [ ] La notification quand l'application est fermée n'existe pas encore (M-5).

## I. Tarifs, forfaits et caisse
Exemples de formules déjà créés dans la base de développement (prix de démonstration, à modifier dans Tarifs).
- [ ] Console staff > Tarifs (compte `admin`) : créer une formule de chaque type ; l'aperçu client change selon le type ; modifier un prix ; retirer une formule de la vente (elle disparaît de `/cafe` et de la liste de vente).
- [ ] Un compte Staff voit les tarifs mais ne peut pas les modifier.
- [ ] Site > Le café : la grille tarifaire s'affiche, groupée par type, avec les prix en euros.
- [ ] Console staff > Comptes > Vendre : choisir une formule et un moyen de paiement, valider : des heures s'ajoutent au compte (pack), ou un forfait apparaît (Mon compte sur le site, accueil du mobile, onglet Profil du client PC).
- [ ] Console staff > Caisse : la vente apparaît avec l'heure, le joueur, le prix, le moyen de paiement et le staff ; les totaux par moyen sont justes ; changer de jour.
- [ ] Forfait illimité (avec plafond par jour) : connectez-vous sur un PC, jouez quelques minutes : le solde en heures ne baisse pas, le forfait oui.
- [ ] Forfait d'heures : le temps restant baisse et, une fois le quota épuisé, le joueur est déconnecté (s'il n'a pas d'heures achetées).
- [ ] Deux forfaits du même type vendus au même joueur : le second commence à la fin du premier.
- [ ] Pas encore possible : annuler ou rembourser une vente (à corriger à la main avec « + min » avec un nombre négatif et une note).

## J. Messagerie (M-4)
Comptes de test : `copain` (joue à tous les jeux, règles acceptées), `alerteur`, `staffeur`. Les mots de passe sont `motdepassetest`.
- [ ] Mobile, onglet Messages, premier passage : les règles s'affichent ; rien d'autre n'est accessible avant « J'accepte ».
- [ ] Choisir des jeux (pastilles jaunes) : un salon par jeu apparaît ; les joueurs qui partagent un jeu sont suggérés (`copain`).
- [ ] Salon : écrire un message ; sur un second téléphone ou via `copain`, il apparaît en direct. Quitter et revenir : l'historique est là.
- [ ] Discussion privée avec un joueur suggéré : les messages arrivent en direct des deux côtés.
- [ ] Liens, numéros de téléphone, message vide, message trop long : refusés avec un message clair. Plus de 6 messages en 10 secondes : « Doucement ».
- [ ] Toucher un message d'un autre joueur : « Signaler ce message » (motif demandé) et « Bloquer ... ».
- [ ] Bloquer un joueur : ses messages disparaissent du salon, il ne peut plus vous écrire, vous ne vous voyez plus dans les suggestions ; « Débloquer » dans la liste en bas de la page Messages.
- [ ] Console staff, page Modération : le signalement arrive avec le message, l'auteur, le motif et le contexte (salon ou privé).
- [ ] « Masquer le message » : il disparaît pour tout le monde. « Silence 24 h » : le joueur voit « vous ne pouvez pas écrire jusqu'au ... ». « Bannir » : plus aucun accès à la messagerie. « Lever la sanction » dans la liste du bas.
- [ ] Un joueur qui n'a pas accepté les règles n'apparaît pas dans les suggestions des autres.
- [ ] Supprimer un compte (console staff) : ses messages disparaissent.
- [ ] Pas de notification quand l'application est fermée (prévu en M-5).

## K. Âge minimum, accord parental, conservation (voir aussi `docs/DONNEES-PERSONNELLES.md`)
- [ ] Inscription sans date de naissance : refusée (site, mobile, client PC). Moins de 12 ans : « Le café est réservé aux 12 ans et plus ». Exactement 12 ans aujourd'hui : accepté.
- [ ] Inscription d'un joueur de 14 ans (moins de 15 ans) : message « un parent doit passer au comptoir » ; connexion possible, mais sur le PC « Accord parental requis », pas de messagerie, pas d'inscription à un événement.
- [ ] Console staff > Comptes : la colonne Âge et l'étiquette « ACCORD PARENTAL À VALIDER » apparaissent ; « Accord reçu » demande une seconde confirmation (« un parent est présent »), puis le joueur peut jouer et écrire.
- [ ] Mobile : le bandeau jaune « Accord parental requis » disparaît seul (en quelques secondes) quand le staff valide.
- [ ] Un joueur de 15 ans révolus (ou de 17 ans) n'a besoin d'aucun accord : pas d'étiquette « ACCORD PARENTAL À VALIDER » dans la console, il peut jouer et écrire tout de suite.
- [ ] Console staff : l'étiquette « accord parental enregistré » n'apparaît que pour un joueur de moins de 15 ans dont l'accord a été saisi.
- [ ] Création d'un compte par le staff : la date de naissance est obligatoire.
- [ ] Un joueur de moins de 15 ans avec accord et un adulte peuvent s'écrire en privé s'ils partagent un jeu.
- [ ] Banni de la messagerie, ou jeu retiré de « Mes jeux » : l'écran de salon ouvert ne reçoit plus les nouveaux messages immédiatement.
- [ ] Les messages de plus de 90 jours disparaissent seuls (testé automatiquement ; la tâche tourne toutes les 6 heures).

## Ce qui n'est PAS encore fait
Notifications poussées et publication de l'application (M-5), mot de passe oublié et suppression de compte par le joueur (W-5, RGPD), installateur du client (PC-7), hébergement du site (W-5), PostgreSQL, paiement en ligne, annulation d'une vente.
