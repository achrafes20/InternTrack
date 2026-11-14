# StageFlow - Planification Scrum (4 Sprints de 2 semaines)

Ce backlog Scrum est structuré pour être directement importé ou géré dans **GitHub Projects**.
Chaque sprint livre un incrément vertical testé (Backend + Frontend + DevOps).

---

## 🎯 Sprint 1 : Socle, Modèle de Données, Sécurité JWT & CI/Docker (Semaines 1-2)
**Objectif de sprint (Sprint Goal)** : Disposer de la chaîne CI/Docker locale, de la base PostgreSQL provisionnée avec EF Core, de l'authentification sécurisée (Access Token en mémoire + Refresh Token en cookie HttpOnly) et de l'interface de connexion UI5.

### US 1.1 : Modélisation du Domaine & Persistance EF Core
- **Description** : Définir les entités (`AcademicTrack`, `Company`, `Internship`, `InternshipOffer`, `ProgressReport`, `ReportComment`, `Defense`, `DefenseJuryMember`, `Convention`, `RefreshToken`, `AuditLog`) avec Fluent API.
- **Critères d'acceptation** :
  - Entités dérivées de `BaseAuditableEntity` (`CreatedAt`, `LastModifiedAt`, `CreatedBy`, `LastModifiedBy`).
  - Index uniques posés (ex: Email, Code de convention, Siren/Siret entreprise).
  - DbContext configuré avec le seed (Filières ENSA, Rôles prédéfinis, Administrateur par défaut).
  - Migration EF Core initiale exécutée avec succès sur PostgreSQL.

### US 1.2 : Authentification JWT, Refresh Token en Cookie HttpOnly & PBKDF2
- **Description** : Implémenter l'authentification avec Identity (hash PBKDF2 par défaut), émission d'un Access Token (durée 15 min, retourné dans le corps JSON) et d'un Refresh Token (durée 7 jours, stocké dans un cookie `HttpOnly; SameSite=Lax` avec `Secure` activé en HTTPS).
- **Critères d'acceptation** :
  - `POST /api/v1/auth/login` émet l'access token et pose le cookie de refresh.
  - `POST /api/v1/auth/refresh` lit le cookie HttpOnly, révoque l'ancien refresh token, émet une nouvelle paire (Refresh Token Rotation).
  - `POST /api/v1/auth/logout` révoque le refresh token en base et supprime le cookie.
  - Rate limiting activé sur le endpoint de login.

### US 1.3 : Gestion des Utilisateurs et Rôles par l'Administrateur
- **Description** : Permettre à l'Administrateur de créer des comptes utilisateurs (Étudiants, Encadrants, Responsables de filière), d'assigner les rôles et d'associer les filières académiques.
- **Critères d'acceptation** :
  - Endpoints `GET /api/v1/users`, `POST /api/v1/users`, `PUT /api/v1/users/{id}/roles`.
  - Contrôle strict via policy `RequireAdminRole`.

### US 1.4 : Frontend UI5 - Shell, Authentification & Gestion de Session
- **Description** : Mettre en place le Layout SAP UI5 Fiori (`ShellBar`, navigation latérale réactive), la page de connexion, le contexte d'authentification (`AuthContext` conservant l'access token en mémoire JS) et l'intercepteur Axios pour le silent-refresh automatique.
- **Critères d'acceptation** :
  - Page de login stylée avec les composants SAP UI5.
  - Intercepteur Axios interceptant les erreurs 401 pour déclencher `POST /api/v1/auth/refresh` sans déconnecter l'utilisateur si le refresh token est valide.
  - Redirection automatique vers la page de login lors de l'expiration du refresh token.

### US 1.5 : DevOps - CI GitHub Actions & Docker Compose Local
- **Description** : Automatiser l'intégration continue et fournir un environnement local reproductible en une commande.
- **Critères d'acceptation** :
  - `docker-compose.yml` orchestrant l'API, PostgreSQL et le client React.
  - Workflow GitHub Actions (`.github/workflows/ci.yml`) exécutant le build .NET, les tests xUnit, et le lint/build du frontend React.

---

## 🎯 Sprint 2 : Offres de Stage, Workflow des Dossiers & IHM Métier (Semaines 3-4)
**Objectif de sprint (Sprint Goal)** : Permettre la gestion complète des entreprises et offres, la soumission des dossiers de stage par les étudiants, et leur validation/refus par les responsables avec suivi visuel dans l'IHM UI5.

### US 2.1 : Gestion et Recherche des Offres de Stage (Backend + Frontend)
- **Description** : CRUD des entreprises et des offres de stage avec filtres multicritères et pagination. Interface étudiante de consultation et formulaire de publication pour l'Admin/Responsable.
- **Critères d'acceptation** :
  - `GET /api/v1/offers` avec recherche textuelle, entreprise, ville, durée, technologies, pagination.
  - Formulaires React Hook Form + Zod pour la saisie côté client.
  - Catalogue d'offres en cartes SAP UI5 (`AnalyticalCard` / `List`) avec pagination et filtres interactifs.

### US 2.2 : Cycle de Vie du Dossier de Stage (Machine à États Backend)
- **Description** : Gestion du cycle de vie d'un stage : `Brouillon` → `Propose` → `Valide` (ou `Refuse`) → `EnCours` → `SoutenancePlanifiee` → `Termine`.
- **Critères d'acceptation** :
  - Soumission d'une proposition par l'étudiant (rattachée à une entreprise existante ou nouvelle).
  - Validation ou Refus motivé (`RejectionReason` obligatoire) réservé au Responsable de filière.
  - Le passage à `EnCours` est déclenché dès la validation et le dépôt des pièces justificatives.
  - Pattern `Result` retournant des erreurs métier claires sans lancer d'exceptions pour le flux normal.

### US 2.3 : Frontend UI5 - Parcours Étudiant & Responsable de Filière
- **Description** : Pages dédiées dans l'application React pour déposer un dossier, visualiser la timeline d'avancement du statut (composant Timeline UI5) et écran de validation des stages pour le Responsable.
- **Critères d'acceptation** :
  - Vue détaillée du dossier de stage avec badge de statut aux couleurs Fiori.
  - Dialogue de refus avec saisie obligatoire du motif pour le responsable.
  - Mises à jour en direct de l'état grâce aux mutations TanStack Query.

---

## 🎯 Sprint 3 : Rapports, Soutenances sans Conflit & Convention PDF QR (Semaines 5-6)
**Objectif de sprint (Sprint Goal)** : Assurer le suivi hebdomadaire avec commentaires, la génération de convention PDF certifiée par QR Code, et la planification des soutenances avec détection algorithmique de conflits.

### US 3.1 : Suivi Pédagogique - Rapports Hebdomadaires & Retours d'Encadrement
- **Description** : Dépôt périodique des rapports d'avancement par l'étudiant, consultation et ajout de commentaires d'évaluation par l'encadrant ENSA.
- **Critères d'acceptation** :
  - `POST /api/v1/internships/{id}/reports` et `POST /api/v1/reports/{id}/comments`.
  - Statuts du rapport : `ARelire` / `Valide`.
  - Interface de feed de commentaires et upload/saisie des livrables en UI5.

### US 3.2 : Planification des Soutenances avec Détection de Conflits
- **Description** : Module de programmation d'une soutenance (date, créneau, salle, jury de 2 à 3 membres) avec vérification de non-collision.
- **Critères d'acceptation** :
  - Détection automatique et blocage si la salle est déjà réservée sur le créneau `[DateDebut, DateFin]`.
  - Détection automatique et blocage si un enseignant du jury est déjà mobilisé dans une autre soutenance concurrente.
  - Couverture par tests unitaires xUnit couvrant tous les cas limites de chevauchement d'intervalles temporels.
  - Calendrier UI5 affichant les soutenances programmées.

### US 3.3 : Génération de Convention PDF (QuestPDF) & QR Code d'Authenticité
- **Description** : Génération à la volée du document officiel de convention en PDF avec inclusion d'un QR code pointant vers une page de vérification publique.
- **Critères d'acceptation** :
  - Mise en page professionnelle via QuestPDF reprenant les informations légales du stage.
  - QR Code matriciel intégrant l'URL dynamique : `{FrontendBaseUrl}/verify-convention/{token}` lue depuis la configuration.
  - Endpoint public et page React publique (sans authentification) affichant le statut d'authenticité et les données non confidentielles (Étudiant, Entreprise, Période).

---

## 🎯 Sprint 4 : Ledger Immuable, IA, Notifications, Dashboard, Azure & README (Semaines 7-8)
**Objectif de sprint (Sprint Goal)** : Finaliser la traçabilité immuable (Ledger append-only), les alertes en arrière-plan, le tableau de bord avec KPIs, le bonus IA Gemini, le déploiement Azure et le dossier de soutenance/README.

### US 4.1 : Journal d'Audit Append-Only (Intercepteur EF Core + Verrouillage PostgreSQL)
- **Description** : Traçabilité inviolable de chaque action critique (changement de statut, validation de convention, planification).
- **Critères d'acceptation** :
  - `AuditLedgerInterceptor` EF Core interceptant chaque modification et insérant un enregistrement dans `AuditLogs`.
  - Verrouillage en base PostgreSQL via Trigger `BEFORE UPDATE OR DELETE` interdisant formellement toute modification ou suppression sur la table `AuditLogs` (garantie Append-Only au niveau SQL).
  - Écran d'administration UI5 avec filtres (par utilisateur, par entité, par plage de dates).

### US 4.2 : Service d'Arrière-Plan (BackgroundService) & Notifications E-mail
- **Description** : `BackgroundService` tournant à intervalle régulier pour détecter les échéances critiques et envoyer des notifications par e-mail.
- **Critères d'acceptation** :
  - Détection des rapports hebdomadaires non soumis dans les délais.
  - Rappel automatique aux jurys et étudiants 48 heures avant une soutenance.
  - Abstraction `IEmailSender` avec `FakeEmailSender` en Dev et `SmtpEmailSender` (MailKit) en Prod.

### US 4.3 : Tableau de Bord Analytique (Dashboard & KPIs)
- **Description** : Endpoint agrégeant les statistiques décisionnelles et affichage sur l'IHM UI5.
- **Critères d'acceptation** :
  - `GET /api/v1/dashboard/stats` retournant : répartition des stages par statut, répartition par entreprise partenaire, stages par filière ENSA, soutenances à venir dans les 7 jours.
  - Composants graphiques et cartes d'indicateurs (KPIs) sur la page d'accueil de l'application.

### US 4.4 : Bonus IA - Résumé de Rapport d'Avancement (Gemini API)
- **Description** : Synthétiser automatiquement les points clés des rapports hebdomadaires volumineux pour faciliter la lecture de l'encadrant.
- **Critères d'acceptation** :
  - Service `IAiSummaryService` appelant l'API Gemini avec clé lue exclusivement via la configuration (User Secrets / Variables d'environnement).
  - Bouton UI5 "Résumer avec l'IA" dans la vue du rapport affichant un encart synthétique.

### US 4.5 : DevOps Azure, Health Checks & Documentation de Production (README)
- **Description** : Mise en production sur Azure, sondes de santé et documentation d'excellence pour recruteur.
- **Critères d'acceptation** :
  - Health checks `/health` et `/health/ready` vérifiant la connectivité PostgreSQL.
  - Déploiement continu Azure Web App (API) et Azure Static Web Apps (Frontend) configuré dans `.github/workflows/cd-azure.yml`.
  - README professionnel complet, schéma d'architecture, instructions de lancement en 3 commandes, guide des choix techniques, texte pour CV/LinkedIn et 10 questions/réponses d'entretien.
