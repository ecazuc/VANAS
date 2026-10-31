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
| `testeur` | `motdepassetest` | Client | |

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

## Ce qui n'est PAS encore fait
Inscription aux événements depuis le site (W-4, il faut d'abord les comptes clients sur le web), QR code et application mobile (M-1 à M-5), comptes clients sur le site (W-4), messagerie (M-4), application mobile (M-1 à M-5), installateur du client (PC-7), hébergement du site (W-5), PostgreSQL, tarifs et forfaits, paiement.
