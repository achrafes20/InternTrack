# Scénario de recette StageFlow — « Une journée à l'ENSA »

Ce scénario couvre le parcours complet de l'application : authentification, workflow des stages,
rapports hebdomadaires, convention PDF et vérification publique, soutenances avec détection de
conflits, administration, audit immuable et contrôles de sécurité.

- **Durée estimée :** 20 à 30 minutes
- **Environnement :** Docker Compose (`http://localhost:3000`, API `http://localhost:5000`)
- **Comptes :** voir le tableau ci-dessous (également disponibles en un clic sur la page de connexion)

| Rôle | E-mail | Mot de passe |
| :--- | :--- | :--- |
| Administrateur | `admin@ensa.ma` | `Admin@123456` |
| Responsable de filière GI | `responsable.gi@ensa.ma` | `Resp@123456` |
| Encadrant pédagogique | `prof.tazi@ensa.ma` | `Prof@123456` |
| Élève ingénieur — Achraf | `etudiant.achraf@ensa.ma` | `Student@123456` |
| Élève ingénieure — Fatima | `etudiant.fatima@ensa.ma` | `Student@123456` |

> **Important — limiteur de connexion :** le endpoint de login est limité à **5 requêtes par minute
> et par IP**, y compris les connexions réussies. Si un `429 Too Many Requests` apparaît, attendre
> une minute ou exécuter `docker compose restart stageflow-api`.

---

## 0. Préparation et remise à zéro des données

Pour rejouer le scénario depuis l'état initial du seed (⚠️ supprime la base PostgreSQL) :

```powershell
docker compose down -v
docker compose up -d
docker compose ps
```

Attendre que `stageflow-api` et `stageflow-db` soient `healthy`.

Vérifications de démarrage :

```powershell
# API + base de données
Invoke-WebRequest http://localhost:5000/health -UseBasicParsing | Select-Object StatusCode, Content

# Client web
Invoke-WebRequest http://localhost:3000 -UseBasicParsing | Select-Object StatusCode
```

**Résultat attendu :** `200` sur les deux URLs, healthcheck `npgsql: Healthy`, trois conteneurs
`Up` (API et DB `healthy`).

---

## 1. Smoke test et contrôle d'accès

1. Ouvrir `http://localhost:3000` sans être connecté : redirection vers `/login`.
2. Ouvrir directement `http://localhost:3000/users` : redirection vers `/login`.
3. Se connecter avec le bouton **« Étudiant (Achraf) »** de la section
   **« Connexion rapide — Comptes de démonstration »**.
4. Vérifier le menu : `Tableau de bord`, `Offres de stage`, `Mes Stages & PFE`,
   `Rapports & Suivi`, `Soutenances`. Les entrées `Utilisateurs` et `Journal d'audit` sont absentes.
5. Tenter manuellement `http://localhost:3000/users` : redirection vers le tableau de bord.

**Résultat attendu :** aucune page d'administration n'est accessible à un étudiant.

---

## 2. Parcours élève ingénieur — Achraf

### 2.1 Tableau de bord

1. Observer la bannière « Bonjour, Achraf Bennani » et le rôle « Élève Ingénieur (5A) ».
2. Vérifier les KPI et les actions rapides : `Nouveau dossier de stage`, `Consulter les offres`,
   `Soumettre un rapport`, `Ma soutenance`.
3. Vérifier que les données affichées concernent uniquement Achraf (2 dossiers initiaux :
   un stage Capgemini **En cours** et un stage OCP **Validé**), pas les stages de Fatima ou Mehdi.

**Résultat attendu :** périmètre de données filtré par rôle (pas de fuite inter-étudiants).

### 2.2 Catalogue des offres

1. Aller dans `Offres de stage` : 3 offres sont visibles (Capgemini, CGI, OCP).
2. Filtrer par mot-clé `React`, par ville `Rabat`, puis par filière `GI`.
3. Réinitialiser les filtres.

**Résultat attendu :** la liste se réduit correctement et le bouton `Réinitialiser` restaure les 3 offres.

### 2.3 Convention PDF

1. Dans `Mes Stages & PFE`, repérer le stage **Capgemini — Architecture Microservices…**
   au statut `En cours`.
2. Cliquer sur **« Télécharger la convention PDF »**.
3. Ouvrir le PDF : en-tête ENSA / StageFlow, informations du stage, signatures et QR code.

**Résultat attendu :** un fichier `convention_<id>.pdf` est téléchargé et s'ouvre correctement.

### 2.4 Vérification publique de la convention (sans compte)

Récupérer un jeton de vérification depuis la base :

```powershell
docker exec stageflow-db psql -U stageflow_user -d stageflow_db -t -A -c 'SELECT "VerificationToken" FROM "Conventions" LIMIT 1;'
```

1. Ouvrir dans une fenêtre de navigation privée :
   `http://localhost:3000/verify-convention/<TOKEN>`
2. Vérifier l'affichage « CONVENTION CERTIFIÉE & AUTHENTIQUE », le numéro de convention,
   l'étudiant, la filière, l'entreprise et la période.
3. Tester un jeton invalide :
   `http://localhost:3000/verify-convention/token-bidon`

**Résultat attendu :** le jeton valide affiche les métadonnées non sensibles ; le jeton invalide
affiche « DOCUMENT NON CERTIFIÉ ». Aucun login n'est demandé.

### 2.5 Rapports hebdomadaires

1. Aller dans `Rapports & Suivi`.
2. Vérifier les rapports existants : semaine 1 (`Validé`) et semaine 2 (`Soumis`).
3. Cliquer sur **« Déposer un rapport »**, saisir :
   - Semaine : `3`
   - Titre : `Observabilité et supervision Prometheus/Grafana`
   - Tâches accomplies : `Mise en place des dashboards et des alertes`
   - Objectifs : `Tests de charge et documentation`
   - Difficultés : `Ajustement des seuils d'alerte`
4. Valider : le rapport semaine 3 apparaît au statut `Soumis`.
5. Rouvrir le formulaire et tenter de déposer à nouveau la semaine `3`.

**Résultat attendu :** le doublon est refusé avec le message
« Un rapport pour la semaine 3 existe déjà. »

### 2.6 Création et soumission d'un stage spontané

1. Dans `Mes Stages & PFE`, cliquer sur **« Proposer un stage spontané »**.
2. Remplir les 4 sections (titre, description, entreprise, filière, tuteur, dates) puis
   **« Créer le dossier »**.
3. Le nouveau dossier apparaît en `Brouillon`.
4. Cliquer sur **« Soumettre »** : le statut passe à `Proposé`.

**Résultat attendu :** le dossier est créé puis soumis. Il servira au test de refus par le
Responsable de filière.

5. Se déconnecter.

---

## 3. Parcours Responsable de filière — validation, refus, démarrage

1. Se connecter avec le bouton **« Responsable GI »**.
2. Vérifier le tableau de bord : les indicateurs portent sur la filière GI et non sur toute l'école.
3. Aller dans `Gestion des Stages`. Trois dossiers sont visibles : Fatima (CGI, `Proposé`),
   Achraf (OCP, `Validé`) et le stage spontané créé à l'étape 2.6 (`Proposé`).
4. Sur le stage spontané d'Achraf, cliquer sur **« Refuser »**, saisir un motif explicite
   (ex. « Sujet trop proche d'un stage déjà validé »), puis **« Confirmer le refus »**.
5. Sur le dossier de Fatima (CGI), cliquer sur **« Valider »** puis **« Confirmer la validation »**.
6. Sur le dossier OCP d'Achraf au statut `Validé`, cliquer sur **« Démarrer le stage »**.

**Résultat attendu :**

- le stage spontané passe à `Refusé` et le motif est visible ;
- le dossier de Fatima passe à `Validé` ;
- le stage OCP d'Achraf passe à `En cours`.

7. Vérifier qu'un refus sans motif est bloqué (le bouton de confirmation doit rester inutilisable
   ou l'API doit renvoyer une erreur explicite).

---

## 4. Parcours Encadrant — suivi pédagogique

1. Se déconnecter puis se connecter avec le bouton **« Encadrant »** (`prof.tazi@ensa.ma`).
2. Aller dans `Rapports & Suivi` :
   - le rapport semaine 2 d'Achraf est `Soumis` ;
   - le rapport semaine 3 créé à l'étape 2.5 est également visible.
3. Sur le rapport semaine 2, cliquer sur **« Valider »** : le badge passe à `Validé`.
4. Sur le rapport semaine 3, cliquer sur **« Commenter »**, saisir un feedback pédagogique et
   envoyer : le commentaire apparaît dans « Voir les détails ».
5. Cliquer sur **« Résumé IA »** : sans clé Gemini configurée, le mode fallback doit renvoyer
   une synthèse commençant par `[Synthèse Automatique]` (ce n'est pas une erreur).
6. Aller dans `Soutenances` : l'encadrant voit la soutenance de Mehdi (dont il est examinateur).
7. Aller dans `Gestion des Stages` : seuls les dossiers qu'il encadre (Achraf, Mehdi) sont visibles.

**Résultat attendu :** validation, commentaire et synthèse fonctionnent ; le périmètre de
l'encadrant est limité à ses étudiants.

8. Se déconnecter.

---

## 5. Parcours Responsable — planification de soutenance et conflits

1. Se reconnecter avec **« Responsable GI »**.
2. Aller dans `Soutenances` : la soutenance de Mehdi est déjà planifiée (généralement à J+5,
   salle `Salle des Thèses B204`, jury `Karim Alami` et `Mohamed Tazi`).
3. Cliquer sur **« Planifier une soutenance »**.
4. Sélectionner le stage Capgemini d'Achraf (`En cours`).
5. Saisir **exactement la même date/heure** que la soutenance de Mehdi, la même salle
   `Salle des Thèses B204`, et les mêmes membres de jury.
6. Cliquer sur **« Vérifier les conflits »**.

**Résultat attendu :** conflit détecté (salle déjà réservée + enseignants déjà mobilisés),
message(s) explicite(s), bouton **« Confirmer la soutenance »** désactivé.

7. Modifier le créneau pour un horaire libre (par exemple +1 heure) et une salle libre
   (par exemple `Salle 204`), puis vérifier à nouveau : « Créneau disponible ».
8. Confirmer la soutenance.

**Résultat attendu :** la soutenance d'Achraf apparaît dans le tableau, avec sa salle et son jury ;
son stage passe à `Soutenance planifiée`.

9. Se déconnecter.

---

## 6. Vérification côté étudiant

1. Se reconnecter avec **« Étudiant (Achraf) »**.
2. Dans `Mes Stages & PFE`, le stage Capgemini est désormais `Soutenance planifiée`.
3. Dans `Soutenances`, Achraf ne voit **que sa propre soutenance**, pas celle de Mehdi.
4. Dans `Tableau de bord`, l'action rapide **« Ma soutenance »** mène bien au planning.
5. Tenter d'ouvrir `http://localhost:3000/users` : redirection vers le tableau de bord.

**Résultat attendu :** isolation des données et navigation cohérente.

6. Se déconnecter.

---

## 7. Parcours Administrateur — utilisateurs, audit et sécurité

1. Se connecter avec le bouton **« Administrateur »**.
2. Vérifier que le tableau de bord présente les données globales de l'établissement.
3. Aller dans `Utilisateurs`, cliquer sur **« Nouvel utilisateur »**, créer :
   - Prénom : `Yasmine`
   - Nom : `Bennani`
   - E-mail : `yasmine.bennani@ensa.ma`
   - Rôle : `Encadrant`
   - Mot de passe : `Password123!`
4. Vérifier que l'utilisateur apparaît dans la liste et que les compteurs par rôle sont mis à jour.
5. Aller dans `Journal d'audit` : vérifier la présence des événements générés par les étapes
   précédentes (création/modification de stages, rapports, commentaires, soutenance, utilisateur).
6. Utiliser les filtres par action, entité, utilisateur et plage de dates.

**Résultat attendu :** les logs sont horodatés, traçables et filtrables.

### 7.1 Vérifier l'immuabilité du journal d'audit

Tenter une modification directe en base :

```powershell
docker exec stageflow-db psql -U stageflow_user -d stageflow_db -c 'UPDATE "AuditLogs" SET "Action" = ''HACK'' WHERE "Id" = (SELECT "Id" FROM "AuditLogs" LIMIT 1);'
```

**Résultat attendu :** erreur PostgreSQL du type
`AuditLogs table is strictly append-only. UPDATE and DELETE operations are forbidden.`
La ligne n'est pas modifiée.

---

## 8. Tests de sécurité complémentaires

### 8.1 Anti-IDOR (API)

Récupérer l'identifiant du stage d'Achraf et le token de Fatima :

```powershell
$achrafInternshipId = (docker exec stageflow-db psql -U stageflow_user -d stageflow_db -t -A -c 'SELECT "Id" FROM "Internships" WHERE "Title" LIKE ''Architecture Microservices%'' LIMIT 1;').Trim()

$login = Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/v1/auth/login `
  -ContentType 'application/json' `
  -Body (@{ email = 'etudiant.fatima@ensa.ma'; password = 'Student@123456' } | ConvertTo-Json)

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }
```

Tester l'accès aux ressources d'Achraf avec le token de Fatima :

```powershell
try {
  Invoke-WebRequest "http://localhost:5000/api/v1/reports/internship/$achrafInternshipId" -Headers $headers -UseBasicParsing
} catch { "Rapports : HTTP $([int]$_.Exception.Response.StatusCode)" }

try {
  Invoke-WebRequest "http://localhost:5000/api/v1/conventions/internship/$achrafInternshipId/pdf" -Headers $headers -UseBasicParsing
} catch { "Convention : HTTP $([int]$_.Exception.Response.StatusCode)" }

try {
  Invoke-WebRequest "http://localhost:5000/api/v1/users" -Headers $headers -UseBasicParsing
} catch { "Utilisateurs : HTTP $([int]$_.Exception.Response.StatusCode)" }
```

**Résultat attendu :** `403 Forbidden` pour les trois appels.

### 8.2 Rotation du refresh token

1. Ouvrir les outils de développement du navigateur (F12) → onglet `Network`.
2. Se connecter : la réponse `/auth/login` contient `accessToken` et un cookie
   `stageflow_refresh_token` (`HttpOnly`, `SameSite=Lax`).
3. Recharger la page : un appel `/auth/refresh` doit renvoyer un nouveau cookie
   (valeur différente) et un nouvel access token.
4. Tenter de réutiliser l'ancien cookie : l'API doit le refuser et révoquer la famille de tokens.

**Résultat attendu :** rotation effective, pas de token réutilisable.

### 8.3 Rate limiting du login

1. Attendre une minute après les connexions précédentes (ou redémarrer l'API).
2. Dans une fenêtre privée, tenter 6 connexions avec un mot de passe faux.

**Résultat attendu :** les premières tentatives renvoient `401`, puis la limite renvoie
`429 Too Many Requests`. Redémarrer l'API réinitialise le compteur :

```powershell
docker compose restart stageflow-api
```

---

## 9. Rapport de recette

| # | Cas de test | Attendu | OK |
| :--- | :--- | :--- | :--- |
| 1 | Healthcheck API + client | `200`, base `Healthy` | ☐ |
| 2 | Accès non authentifié | Redirection `/login` | ☐ |
| 3 | Navigation étudiant | Pas de menu admin | ☐ |
| 4 | Dashboard étudiant | Données limitées à Achraf | ☐ |
| 5 | Offres + filtres | 3 offres, filtres fonctionnels | ☐ |
| 6 | Convention PDF | Téléchargement + ouverture | ☐ |
| 7 | Vérification publique | Convention certifiée / jeton invalide refusé | ☐ |
| 8 | Dépôt rapport semaine 3 | Rapport `Soumis` | ☐ |
| 9 | Doublon de rapport | Message « existe déjà » | ☐ |
| 10 | Stage spontané | `Brouillon` → `Proposé` | ☐ |
| 11 | Refus avec motif | Statut `Refusé` + motif | ☐ |
| 12 | Validation Fatima | Statut `Validé` | ☐ |
| 13 | Démarrage stage OCP | Statut `En cours` | ☐ |
| 14 | Validation rapport encadrant | Badge `Validé` | ☐ |
| 15 | Commentaire pédagogique | Visible dans le détail | ☐ |
| 16 | Résumé IA (fallback) | Texte `[Synthèse Automatique]` | ☐ |
| 17 | Conflit salle/jury | Conflit détecté, confirmation bloquée | ☐ |
| 18 | Créneau libre | Soutenance créée | ☐ |
| 19 | Étudiant voit sa soutenance | Une seule ligne, la sienne | ☐ |
| 20 | Création utilisateur admin | Utilisateur listé | ☐ |
| 21 | Journal d'audit | Événements présents et filtrables | ☐ |
| 22 | Immuabilité audit | `UPDATE` refusé par PostgreSQL | ☐ |
| 23 | Anti-IDOR | `403` sur les ressources d'autrui | ☐ |
| 24 | Rotation refresh token | Nouveau cookie, ancien refusé | ☐ |
| 25 | Rate limiting login | `429` après 5 tentatives/minute | ☐ |

---

## 10. Nettoyage

Pour arrêter la pile :

```powershell
docker compose down
```

Pour repartir d'une base vierge (suppression des données de test) :

```powershell
docker compose down -v
docker compose up -d
```
