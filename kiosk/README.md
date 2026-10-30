# Mode kiosque VANAS (étape PC-2)

Objectif : au démarrage, le PC arrive directement sur l'écran de connexion VANAS, sans bureau Windows accessible.

## Principe (3 couches)

| Couche | Rôle | Où |
|---|---|---|
| **Compte Windows dédié** (`vanas-client`, utilisateur standard) | Aucun droit admin ; VANAS remplace `explorer.exe` pour ce compte uniquement | `Install-KioskUser.ps1` |
| **Restrictions Windows** sur ce compte | Pas de gestionnaire de tâches, cmd, regedit, touches Windows, verrouillage, déconnexion | `Install-KioskUser.ps1` |
| **Verrou dans l'application** | Écran de connexion plein écran toujours au premier plan, Alt+Tab / Win / Alt+F4 / Ctrl+Échap bloqués, fermeture des jeux en fin de session, sortie par code PIN | `VANAS.Client` (`client.json`) |

Votre compte administrateur n'est jamais modifié. Le client ne s'active en mode kiosque **que** s'il tourne sous le compte nommé dans `KioskAccount` : lancé depuis un autre compte, il se comporte normalement.

## Comportement dans le client

- **Écran de connexion** : fenêtre toujours au premier plan, qui reprend le focus si on le perd ; Win, Alt+Tab, Alt+Échap, Alt+F4 et Ctrl+Échap bloqués.
- **Pendant une session** : Alt+Tab libre (pour passer d'un jeu à VANAS), touche Windows bloquée, Alt+F4 bloqué uniquement quand la fenêtre VANAS a le focus (il reste utilisable pour fermer un jeu).
- **Fin de session** (déconnexion, temps écoulé, kick) : retour à l'écran de connexion, et tous les programmes lancés pendant la session (hors Windows) sont fermés.
- **Sortie** : `Ctrl+Maj+F12` puis le code PIN admin (3 erreurs = blocage 1 minute). Sans `AdminPinHash` dans `client.json`, aucune sortie n'est possible.
- Ctrl+Alt+Suppr ne peut pas être bloqué par une application : c'est pourquoi les restrictions Windows (gestionnaire de tâches, verrouillage, changement de mot de passe, déconnexion) sont appliquées au compte.

## Réglages de `client.json`

| Clé | Rôle |
|---|---|
| `KioskMode` | `true` pour activer le verrou (ignoré si le compte Windows ne correspond pas) |
| `KioskAccount` | Nom du compte Windows dédié (défaut `vanas-client`) |
| `AdminPinHash` | Hash du PIN de sortie (voir ci-dessous) |
| `CloseProgramsOnLogout` | `true` en production. `false` pour tester le verrou sans fermer vos propres programmes |

## Installation sur un PC

> **Testez d'abord dans une machine virtuelle Windows** (Hyper-V, VirtualBox...) : un mauvais réglage peut vous enfermer hors d'un PC. Ces scripts n'ont pas encore été exécutés sur une vraie machine.

1. Copiez le dossier du client (build `Release`) sur le PC, par ex. `C:\VANAS\Client\`.
2. Ouvrez PowerShell **en administrateur** dans le dossier `kiosk\`.
3. Générez le PIN : `.\New-AdminPin.ps1 -Pin 482913 -ClientConfig C:\VANAS\Client\client.json`
4. Simulez : `.\Install-KioskUser.ps1 -ClientExe C:\VANAS\Client\VANAS.Client.exe -EnableAutoLogon -WhatIf`
5. Appliquez (sans `-WhatIf`) puis redémarrez : le PC doit ouvrir `vanas-client` et afficher VANAS.
6. Notez le mot de passe généré affiché une seule fois.

Pour annuler : `.\Uninstall-KioskUser.ps1 -RemoveProfile` depuis un autre compte administrateur.

## Se dépanner

- **Sortir du kiosque** : `Ctrl+Maj+F12` + PIN. Si le PIN est perdu : redémarrer, ouvrir une session avec un compte administrateur, et relancer `New-AdminPin.ps1`.
- **Le PC reste sur un écran noir** : le chemin `ClientExe` est faux ou le client a planté. Ouvrir une session avec un autre compte administrateur, corriger ou lancer `Uninstall-KioskUser.ps1`.
- **Tester le verrou sans VM** (à vos risques) : dans une copie du client, mettre `KioskMode: true`, `KioskAccount` = votre nom Windows, `CloseProgramsOnLogout: false` et un `AdminPinHash`. Gardez le PIN sous la main.
