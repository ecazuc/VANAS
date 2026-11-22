# VANAS — guide du design

La référence visuelle est le fichier Figma **VANAS - Design System** : https://www.figma.com/design/zAkx3rIOGFVrqKjxctju44

- Page **Fondations** : couleurs, typographies, espacements.
- Page **Composants** : boutons, champs, cartes, barre de temps, tuiles de jeu, billet, tableau… Chaque composant a sa règle d'usage dans sa description Figma.
- Les maquettes des écrans sont dans le même fichier et utilisent ces composants : modifier un composant ou une variable met à jour tous les écrans.

Ce document garde ce que Figma ne dit pas : les principes, le ton des textes et ce qu'il faut changer dans le code. Le nom et le logo sont provisoires.

## Principes

- **Sombre, toujours.** Un seul thème. Les écrans tournent dans une salle peu éclairée, à côté des jeux.
- **Un seul jaune.** `accent` (`#fcee0a`) est la marque. Il souligne (bordures, titres, liens, focus), il ne remplit pas. Les aplats jaunes sont réservés à un bouton survolé ou à une étiquette d'événement, avec `ink-on-accent` dessus.
- **Angles vifs.** Aucun arrondi (`radius-none`). Des angles coupés à 45° sont proposés (`chamfer-sm` 6, `chamfer-md` 12, `chamfer-lg` 24), pas encore validés ni codés.
- **Lisible de loin.** Le temps restant et l'état d'un poste se lisent à deux mètres : chiffres en police `tech`, grands, en `heading` ou `accent`.
- **Les états ont leur couleur.** `ok` = en jeu, validé, payé. `danger` = erreur, action irréversible, dernière minute. Jamais comme décor.
- **Une seule lueur par écran** (`glow-strong`) : le logo ou un titre accent.

## Ton des textes

- Français. Les libellés courts (boutons, états, en-têtes, menus) en majuscules : `SE CONNECTER`, `EN JEU`, `HORS SESSION`, `ENTRÉE VALIDÉE`.
- Le joueur est tutoyé dans les apps (« Il te reste 10 minutes »). Les pages publiques du site vouvoient.
- Phrases simples et concrètes. Une erreur dit quoi faire : « Mot de passe incorrect. Réessaie ou demande au comptoir. »

## Tokens

Les noms sont les mêmes partout : variables Figma, variables CSS (`--accent`), ressources XAML.

| Token | Valeur | Rôle |
| --- | --- | --- |
| `bg` | `#16181c` | fond de page, fond des champs |
| `panel` | `#23262d` | cartes, en-tête, barre latérale, tableaux |
| `panel-light` | `#2e323b` | survol, filets, champs au repos, désactivé |
| `accent` | `#fcee0a` | la marque |
| `accent-dim` | `#c9be00` | survol ou variante secondaire de l'accent |
| `ink-on-accent` | = `bg` | texte sur un aplat accent |
| `heading` | `#ffffff` | grands titres, valeurs clés |
| `text` | `#c8ccd4` | texte courant |
| `muted` | `#7c828f` | libellés, aides (contraste à revoir, voir plus bas) |
| `danger` | `#ff4d5e` | erreur, danger |
| `ok` | `#3df2a5` | en jeu, validé |

Polices : `tech` = Rajdhani (titres, libellés, boutons, chiffres, espacement des lettres 0,04em) ; `body` = Inter (texte courant). Espacements : 4, 8, 12, 16, 24, 36, 48, 96. Bordures : 1 (`hair`), 2 (`strong`), 3 (`heavy`).

## Ce qu'il faut changer dans le code

- **Client PC** (`VANAS.Client/Themes/Theme.xaml`) : mêmes couleurs, mais police Bahnschrift seule. Embarquer Rajdhani et Inter dans l'app. Ajouter `heading`, `ink-on-accent`, `accent-dim` comme ressources nommées au lieu des valeurs en dur.
- **Mobile** (`VANAS.Mobile/Resources/Styles/Colors.xaml`) : seul `Primary` est en jaune VANAS ; le reste vient du modèle MAUI (violets, Magenta, MidnightBlue) et la police est Open Sans. Remplacer par ces tokens et ces polices.
- **Site et console** (`VANAS.Web/wwwroot/css/vanas.css`) : source actuelle des tokens. Remplacer `#fff` et `#16181c` écrits en dur par `--heading` et `--ink-on-accent`.
- **Contraste** : `muted` donne 4,4:1 sur `bg` et 3,8:1 sur `panel`, sous le seuil de 4,5:1 pour du petit texte. Proposition : `#8b919d` (à valider).
- **Angles coupés** : à décider sur les maquettes avant de les coder (clip-path côté web, géométrie côté WPF et MAUI).
