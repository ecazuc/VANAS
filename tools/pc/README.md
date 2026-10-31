# Installer et mettre à jour le client sur les PC du café (PC-7)

Trois scripts, à lancer dans PowerShell **en administrateur** sur le PC concerné (sauf mention contraire).

| Script | Rôle |
|---|---|
| `tools\Publish-Client.ps1` | Sur le **PC de développement** : compile le client et le publie sur le serveur du café. |
| `Install-VanasClient.ps1` | Sur **chaque PC client** : installe le client, ses réglages, ses journaux et sa mise à jour automatique. |
| `Update-VanasClient.ps1` | Lancé **tout seul** au démarrage du PC (tâche planifiée) : met le client à jour si le serveur a une version plus récente. |
| `Uninstall-VanasClient.ps1` | Retire le client (le compte kiosque se retire avec `kiosk\Uninstall-KioskUser.ps1`). |

## 1. Publier une version (sur le PC de développement)

```powershell
.\tools\Publish-Client.ps1                 # numéro de version lu dans VANAS.Client\VANAS.Client.csproj
.\tools\Publish-Client.ps1 -Version 1.2.0  # ou un numéro choisi
```

Trois fichiers sont écrits dans `VANAS.Server\ClientUpdates` (réglage `Client:UpdateFolder`) : `client.zip`, `client.sha256`, `version.txt`. Le serveur les distribue à l'adresse `/api/client/version` et `/api/client/package`. Le paquet ne contient jamais `client.json` : chaque PC garde ses réglages.

Un PC ne se met à jour que vers un numéro **plus élevé** : pensez à augmenter la version à chaque publication.

## 2. Installer un PC

Le serveur doit tourner et avoir une version publiée. Sur le PC client :

```powershell
# Installation simple (PC de test)
.\Install-VanasClient.ps1 -ServerUrl http://192.168.1.10:5049 -StationName PC-01

# Poste de jeu complet : mode kiosque, code PIN de sortie, ouverture de session automatique
.\Install-VanasClient.ps1 -ServerUrl http://192.168.1.10:5049 -StationName PC-01 `
    -EnableKiosk -Pin 482913 -EnableAutoLogon
```

Le script télécharge le client, vérifie sa somme de contrôle, l'installe dans `C:\VANAS\Client`, écrit `client.json` (adresse du serveur, nom du poste), crée le dossier des journaux et la tâche planifiée « VANAS Client Update » (au démarrage et chaque jour à 4 h). Avec `-EnableKiosk`, il enchaîne les scripts du dossier `kiosk\` (voir `kiosk\README.md`) : **testez-le d'abord dans une machine virtuelle**.

Options utiles : `-InstallDir` (autre dossier), `-SourceZip` (paquet local au lieu du téléchargement), `-Force` (réécrire `client.json`), `-SkipScheduledTask` (essai sans droits administrateur).

## 3. Mise à jour automatique

La tâche interroge le serveur **2 minutes après chaque démarrage** et chaque jour à 4 h. Elle ne fait **rien** si :
- le serveur est éteint ou n'a rien publié (le PC garde sa version) ;
- la version du serveur n'est pas plus récente ;
- un joueur est en session : le client écrit `C:\ProgramData\VANAS\state.json` (« en session » ou non, rafraîchi toutes les 30 secondes) ; un état « en session », absent ou ancien fait attendre le passage suivant.

Sinon elle télécharge le paquet, **vérifie sa somme de contrôle** et le prépare à côté de l'ancienne version pendant que le client tourne. Si personne ne joue, elle arrête le client, échange les dossiers (l'ancienne version est remise si l'échange échoue) et **redémarre le PC** pour lancer la nouvelle version : en mode kiosque, le client est le « bureau » Windows, un redémarrage est la façon propre de le relancer. `client.json` est toujours conservé. Options : `-Force` (réinstaller même sans version plus récente), `-NoRestart`.

Le résultat est écrit dans `C:\ProgramData\VANAS\logs\update.log`.

## 4. Journaux

| Fichier | Contenu |
|---|---|
| `C:\ProgramData\VANAS\logs\client-AAAA-MM-JJ.log` | Démarrage et arrêt du client, début et fin de session (sans nom de joueur), perte et retour du serveur, erreurs. Conservés 14 jours. |
| `C:\ProgramData\VANAS\logs\update.log` | Les mises à jour automatiques. |

Les journaux ne contiennent ni mot de passe, ni jeton, ni nom de joueur.

## Dépannage

- **« Aucune mise à jour publiée »** : lancez `Publish-Client.ps1` sur le PC de développement, et vérifiez que le serveur écoute sur le réseau (`--launch-profile lan`) avec le pare-feu ouvert.
- **Le PC ne se met pas à jour** : lisez `update.log` ; vérifiez que le numéro publié est plus élevé que celui de `C:\VANAS\Client\version.txt`.
- **Retour à la version précédente** : republiez l'ancien code avec un numéro plus élevé (un PC ne revient jamais en arrière tout seul).
