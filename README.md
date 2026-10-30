# VANAS
Logiciel de gestion de cybercafé (client PC kiosque, serveur API, site web, mobile). Voir [PLAN.md](PLAN.md).

## Lancer le serveur (dev)
```
dotnet run --project VANAS.Server --launch-profile http
```
Swagger : http://localhost:5049/swagger. Compte admin de dev : `admin` / `Admin-Dev-123!` (voir `appsettings.Development.json`, à ne jamais utiliser en production : définir `Jwt:Key` et `Seed:AdminPassword` par variables d'environnement).

## Tests
```
dotnet test
```

## Site web et console staff
```
dotnet run --project VANAS.Web --launch-profile http
```
Site public : http://localhost:5100 — console staff : http://localhost:5100/admin (même compte admin que ci-dessus). Le serveur API doit tourner (adresse réglée dans `VANAS.Web/appsettings.json`, clé `Api:BaseUrl`).
