# 🎓 InternTrack — Plateforme Enterprise de Gestion des Stages & PFE

[![.NET 10](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19.0-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178C6?style=for-the-badge&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-3.4-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white)](https://tailwindcss.com/)
[![Font Awesome](https://img.shields.io/badge/Font_Awesome-6.7-528DD7?style=for-the-badge&logo=font-awesome&logoColor=white)](https://fontawesome.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)

**InternTrack** est une plateforme web d'entreprise moderne conçue pour digitaliser, piloter et certifier l'intégralité du cycle de vie des stages et **Projets de Fin d'Études (PFE)** des élèves ingénieurs au sein du réseau des **Écoles Nationales des Sciences Appliquées (ENSA)**.

Elle remplace les échanges dispersés par e-mails et les fichiers Excel par une solution unifiée, sécurisée et conforme aux exigences académiques.

---

## ⚡ Fonctionnalités Clés

- 📋 **Gestion du Cycle de Vie (Workflow State Machine)** : Machine à états finis stricte (`Brouillon` → `Proposé` → `Validé` → `En cours` → `Soutenance` → `Terminé`).
- 📄 **Conventions PDF Vectorielles & QR Code Infalsifiable** : Moteur QuestPDF générant des conventions officielles certifiées par signature électronique et vérifiables publiquement via QR Code (`/verify-convention/:token`).
- 🤖 **Suivi Hebdomadaire & Synthèse IA** : Dépôt structuré des rapports d'avancement par l'élève, validation pédagogique par l'encadrant et résumé intelligent automatique propulsé par **Google Gemini**.
- 📅 **Planification des Soutenances & Anti-Collision** : Algorithme prédictif de détection automatique des conflits de salle et de double-réservation des enseignants membres du jury.
- 🛡️ **Journal d'Audit Réglementaire Immuable (Ledger)** : Traçabilité médico-légale inviolable garantie par un double verrouillage : Intercepteur EF Core + **Trigger PostgreSQL bloquant tout `UPDATE` ou `DELETE`**.
- 🔐 **Sécurité Avancée conforme OWASP** : Authentification hybride par double token (Access Token 15 min en mémoire volatile, Refresh Token tournant dans cookie `HttpOnly` avec hachage SHA-256 en base et détection de rejeu).
- 🎨 **UI/UX Moderne & Professionnelle** : Interface responsive conçue avec **Tailwind CSS**, composants **SAP UI5** et icônes vectorielles **Font Awesome** (zéro emoji, barres de recherche homogènes).

---

## 🚀 Démarrage Rapide avec Docker

### Prérequis
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installé et en cours d'exécution.

```bash
# 1. Cloner le dépôt
git clone https://github.com/achraf/StageFlow.git
cd StageFlow

# 2. Copier le fichier d'environnement
cp .env.example .env

# 3. Lancer la pile applicative (PostgreSQL + API .NET 10 + Client React 19)
docker compose up -d
```

### 🌐 Accès aux applications :
- **Application Web (Frontend)** : [http://localhost:3000](http://localhost:3000)
- **Documentation API (Swagger / OpenAPI)** : [http://localhost:5000/swagger](http://localhost:5000/swagger)

---

## 👥 Comptes de Démonstration (Pré-configurés)

Sur la page de connexion ([http://localhost:3000/login](http://localhost:3000/login)), des puces d'accès rapide permettent de se connecter en un clic :

| Profil | Identifiant (E-mail) | Mot de passe | Rôle & Permissions |
|---|---|---|---|
| 👑 **Administrateur** | `admin@ensa.ma` | `Admin@123456` | Gestion globale des utilisateurs, consultation du registre d'audit immuable |
| 📋 **Responsable Filière (GI)** | `responsable.gi@ensa.ma` | `Resp@123456` | Validation / refus motivé des stages, démarrage et planification des soutenances |
| 👨‍🏫 **Encadrant Pédagogique** | `prof.tazi@ensa.ma` | `Prof@123456` | Suivi des élèves ingénieurs, évaluation des rapports hebdomadaires, résumés IA |
| 🎓 **Élève Ingénieur (Actif)** | `etudiant.achraf@ensa.ma` | `Student@123456` | Stage validé chez Capgemini, téléchargement convention PDF avec QR Code, dépôt de rapports |
| 🎓 **Élève Ingénieur (Candidat)** | `etudiant.fatima@ensa.ma` | `Student@123456` | Consultation des offres, proposition de stage en attente de validation |

---

## 🏗️ Architecture du Projet

Le backend applique les principes de la **Clean Architecture** (Architecture Onion) découplée de l'infrastructure et orientée cas d'utilisation :

```
├── src/
│   ├── StageFlow.Domain/          # Cœur métier : Entités, Enums, Règles du Workflow (zéro dépendance)
│   ├── StageFlow.Application/     # Cas d'utilisation, DTOs, Validation, Pattern Result
│   ├── StageFlow.Infrastructure/  # EF Core 10, PostgreSQL, QuestPDF, JWT, MailKit, Gemini IA
│   ├── StageFlow.Api/             # ASP.NET Core 10 Web API, Middlewares, Rate Limiter, Swagger UI
│   └── stageflow-client/          # React 19 + TypeScript + Vite + Tailwind CSS + Font Awesome
├── tests/
│   ├── StageFlow.Domain.UnitTests/       # Tests unitaires du domaine et de la machine à états
│   ├── StageFlow.Application.UnitTests/  # Tests unitaires des services applicatifs
│   └── StageFlow.Api.IntegrationTests/   # Tests d'intégration API (WebApplicationFactory)
└── docs/                                 # Diagrammes ER, Registre des décisions architecturales (ADR)
```

### Flux Applicatif

```mermaid
graph TD
    Client[React 19 + Tailwind CSS + Font Awesome] -->|HTTP REST + Bearer JWT + Cookie HttpOnly| API[StageFlow.Api - .NET 10]
    API --> App[StageFlow.Application - Services & Pattern Result]
    App --> Domain[StageFlow.Domain - Entités Métier & State Machine]
    App --> Infra[StageFlow.Infrastructure]
    Infra --> DB[(PostgreSQL 16 - Append-Only Trigger)]
    Infra --> PDF[Moteur Vectoriel QuestPDF & QR Code]
    Infra --> AI[Google Gemini AI Service]
    Infra --> Mail[Service Notifications SMTP / MailKit]
```

---

## 🧪 Tests & Qualité de Code

Le projet est entièrement testé à l'aide de **xUnit**, **FluentAssertions** et **Moq** :

```bash
# Exécution de la suite complète de tests (.NET)
dotnet test StageFlow.sln
```

- **Domain Tests** : Couverture exhaustive des invariants temporels, règles de transition et calculs de conflits.
- **Application Tests** : Validation des cas d'utilisation et des flux de données.
- **Integration Tests** : Tests de bout en bout de l'API via `WebApplicationFactory` avec base de données isolée en mémoire.

```bash
# Vérification du typage et build du Frontend
cd src/stageflow-client
npm run build
```

---

## 💻 Développement Local (Sans Docker)

### Backend (.NET 10 API)
```bash
# Depuis la racine du projet
dotnet run --project src/StageFlow.Api
# L'API démarre sur http://localhost:5009 (ou https://localhost:7009)
```

### Frontend (React 19 + Vite)
```bash
cd src/stageflow-client
npm install
npm run dev
# L'interface démarre sur http://localhost:5173
```

---

## 🔐 Bonnes Pratiques & Sécurité

1. **Anti-XSS & Anti-CSRF** : Aucun jeton d'accès n'est stocké dans le `localStorage`. L'access token est volatile en mémoire React, tandis que le refresh token réside dans un cookie `HttpOnly; SameSite=Lax`.
2. **Protection Anti-Brute Force** : Rate limiting configuré sur les endpoints sensibles (authentification limitée à 5 tentatives par minute par IP).
3. **Contrôle d'accès au niveau ressource (Anti-IDOR)** : Chaque ressource (stage, rapport, soutenance) vérifie les habilitations précises de l'utilisateur connecté avant tout retour de données.
4. **Intégrité Cryptographique** : Vérification des conventions par jeton unique non prédictible (GUID cryptosécurisé), sans exposition de données personnelles.

---

## 📄 Licence

Ce projet est sous licence MIT — libre d'utilisation et d'adaptation.
Développé avec rigueur et passion pour l'excellence de l'enseignement supérieur d'ingénierie.
