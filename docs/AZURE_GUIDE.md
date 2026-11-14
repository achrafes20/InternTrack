# Guide de Déploiement Azure - StageFlow

Ce document détaille pas à pas le provisionnement et la configuration des ressources Azure pour déployer StageFlow en production.

---

## 1. Architecture Cible sur Azure

- **API Backend** : Azure App Service (Plan Linux B1 ou supérieur, runtime .NET 10).
- **Base de Données** : Azure Database for PostgreSQL - Flexible Server (Standard B1ms, PostgreSQL 16).
- **Frontend** : Azure Static Web Apps (Free tier) ou Azure App Service Web App.
- **Secrets & Sécurité** : Azure App Service Configuration / Azure Key Vault.
- **CI/CD** : GitHub Actions via publish profile et deployment token.

---

## 2. Déploiement avec Azure CLI

### Étape 2.1 : Connexion et création du Resource Group
```bash
az login
az group create --name rg-stageflow-prod --location westeurope
```

### Étape 2.2 : Provisionnement de PostgreSQL Flexible Server
```bash
az postgres flexible-server create \
  --resource-group rg-stageflow-prod \
  --name psql-stageflow-prod \
  --location westeurope \
  --admin-user stageflow_admin \
  --admin-password 'YourVeryStrongPassword2026!' \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 16 \
  --storage-size 32

# Autoriser les services Azure à accéder à la base de données
az postgres flexible-server firewall-rule create \
  --resource-group rg-stageflow-prod \
  --name psql-stageflow-prod \
  --rule-name AllowAllAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Créer la base de données applicative
az postgres flexible-server db create \
  --resource-group rg-stageflow-prod \
  --server-name psql-stageflow-prod \
  --database-name stageflow_db
```

### Étape 2.3 : Création de l'App Service pour l'API .NET
```bash
# Création du plan App Service Linux
az appservice plan create \
  --name plan-stageflow-prod \
  --resource-group rg-stageflow-prod \
  --sku B1 \
  --is-linux

# Création de l'App Service Web App
az webapp create \
  --resource-group rg-stageflow-prod \
  --plan plan-stageflow-prod \
  --name app-stageflow-api \
  --runtime "DOTNETCORE:10.0"
```

### Étape 2.4 : Configuration des Variables d'Environnement et Chaîne de Connexion
```bash
az webapp config appsettings set \
  --resource-group rg-stageflow-prod \
  --name app-stageflow-api \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    ConnectionStrings__DefaultConnection="Host=psql-stageflow-prod.postgres.database.azure.com;Port=5432;Database=stageflow_db;Username=stageflow_admin;Password=YourVeryStrongPassword2026!;Ssl Mode=Require;" \
    JwtSettings__Secret="YourVeryLongAndSecureSecretKeyGeneratedForProduction2026!" \
    JwtSettings__Issuer="StageFlowEnsa" \
    JwtSettings__Audience="StageFlowClient" \
    JwtSettings__AccessTokenExpirationMinutes="30" \
    JwtSettings__RefreshTokenExpirationDays="7" \
    AppSettings__FrontendBaseUrl="https://agreeable-beach-xxx.azurestaticapps.net" \
    GeminiSettings__ApiKey="AIzaSyYourProductionGeminiApiKeyHere"
```

### Étape 2.5 : Création de l'Azure Static Web App pour le Frontend
```bash
az staticwebapp create \
  --name app-stageflow-client \
  --resource-group rg-stageflow-prod \
  --source https://github.com/<your-username>/StageFlow \
  --location "westeurope" \
  --branch master \
  --app-location "/src/stageflow-client" \
  --output-location "dist"
```

---

## 3. Configuration des Secrets GitHub Actions

Dans votre dépôt GitHub, allez dans **Settings > Secrets and variables > Actions** et configurez :

1. `AZURE_WEBAPP_NAME` : `app-stageflow-api`
2. `AZURE_WEBAPP_PUBLISH_PROFILE` : Téléchargé depuis le portail Azure sur la page de votre Web App (*Get publish profile*).
3. `AZURE_STATIC_WEB_APPS_API_TOKEN` : Obtenu lors de la création de la Static Web App (*Manage deployment token*).

Chaque push sur la branche `master` déclenchera automatiquement le build et le déploiement continu sur Azure.
