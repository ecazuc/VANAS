# VANAS.Mobile (.NET MAUI)

Application mobile des joueurs. Etape M-1 : connexion / creation de compte, stockage securise du jeton, accueil avec le temps restant.

## Prerequis (installes sur Q:)
Le SDK .NET systeme n'a pas les composants MAUI et `C:` manque de place, donc un SDK portable est utilise :

    $env:DOTNET_ROOT="Q:\dotnet-maui"; $env:DOTNET_CLI_HOME="Q:\dotnet-maui\home"
    $env:NUGET_PACKAGES="Q:\dotnet-maui\nuget"; $env:TEMP="Q:\dotnet-maui\tmp"; $env:TMP="Q:\dotnet-maui\tmp"

Installation (deja faite) : `dotnet-install.ps1 -Version 8.0.427 -InstallDir Q:\dotnet-maui` puis `dotnet workload install maui-windows maui-android`.

## Essayer sur le PC (version Windows)
Le serveur doit tourner (`http://localhost:5049`).

    & "Q:\dotnet-maui\dotnet.exe" build -f net8.0-windows10.0.19041.0
    bin\Debug\net8.0-windows10.0.19041.0\win-x64\VANAS.Mobile.exe

## Telephone Android
Necessite l'Android SDK (a installer sur Q:, licences Google a accepter par vous) et un telephone en mode developpeur.
Dans l'application : "Parametres du serveur" -> adresse du PC serveur sur le Wi-Fi du cafe (ex. `http://192.168.1.20:5049`).
Le trafic HTTP en clair est autorise pour le developpement ; il faudra du HTTPS en production (etape W-5).

Ce projet n'est pas dans `VANAS.sln` : le SDK systeme ne sait pas le compiler.
