# Registre des Décisions Architecturales (ADR) - StageFlow

Ce document consigne les décisions techniques et de modélisation prises de manière autonome pour garantir la cohérence et l'excellence de niveau production du projet StageFlow.

---

### ADR 001 : Nommage unifié du dossier de stage (`Internship`)
- **Contexte** : Dans le cycle de vie du projet, un stage passe de la proposition/candidature initiale jusqu'à la soutenance finale.
- **Décision** : L'entité centrale est nommée `Internship` (avec son `InternshipStatus`), plutôt que `InternshipApplication`, ce qui représente l'intégralité du cycle de vie du dossier d'un étudiant.

### ADR 002 : Stockage et rotation des tokens JWT
- **Contexte** : Concilier sécurité contre XSS et CSRF tout en offrant une expérience utilisateur fluide.
- **Décision** : L'Access Token (15 min) est renvoyé dans la réponse JSON et conservé en mémoire volatile (React State). Le Refresh Token (7 jours) est transmis exclusivement dans un cookie `HttpOnly; SameSite=Lax; Path=/api/v1/auth`, avec le flag `Secure` activé automatiquement en HTTPS (indispensable pour le développement local en HTTP). Les tokens sont hachés en SHA-256 en base, avec rotation à chaque renouvellement et révocation de la famille de tokens en cas de réutilisation détectée.

### ADR 003 : Immuabilité du Journal de Traçabilité (Ledger)
- **Contexte** : Les exigences d'audit réglementaires imposent qu'aucune ligne de log ne puisse être modifiée ou supprimée, même accidentellement.
- **Décision** : Double verrou : au niveau EF Core (Intercepteur bloquant toute opération de modification/suppression sur `AuditLog`) et au niveau PostgreSQL via un Trigger `BEFORE UPDATE OR DELETE` qui lève une exception SQL bloquante.

### ADR 004 : Sécurité et dynamique du QR Code
- **Contexte** : Le QR code apposé sur la convention PDF doit permettre de certifier le document publiquement sans dépendance dure à une URL codée en dur.
- **Décision** : L'URL de base publique (`FrontendBaseUrl`) est injectée via les options de configuration. La convention génère un jeton cryptographique aléatoire unique (`VerificationToken`) sans données identifiantes directes, résolu par un endpoint public `GET /api/v1/conventions/verify/{token}`.


### ADR 005 : Accès concurrents
- **Contexte** : Plusieurs responsables peuvent agir en même temps sur les mêmes dossiers, et l'API peut tourner sur plusieurs instances. Une vérification applicative suivie d'une écriture (« vérifier puis écrire ») ne suffit pas à empêcher les doublons.
- **Décision** :
  1. Concurrence optimiste sur toutes les entités métier via la colonne système PostgreSQL `xmin` (aucune colonne ajoutée). Un conflit renvoie HTTP 409.
  2. La planification des soutenances s'exécute dans une transaction protégée par `pg_advisory_xact_lock`. Ce verrou couvre aussi les conflits de jury, qui portent sur plusieurs lignes et ne sont pas exprimables par une contrainte.
  3. Une contrainte d'exclusion `btree_gist` sur `(lower(Classroom), tstzrange(ScheduledAt, EndsAt))` garantit au niveau base qu'une salle n'est jamais réservée deux fois. `EndsAt` est persisté parce que `timestamptz + interval` n'est pas immuable, donc inutilisable dans un index.
- **Conséquence** : l'extension `btree_gist` doit être disponible (incluse dans l'image Docker officielle ; à autoriser explicitement sur Azure Flexible Server).

### ADR 006 : Refresh token, plusieurs onglets et verrouillage de compte
- **Contexte** : la rotation stricte du refresh token prenait deux onglets qui rafraîchissent en même temps pour un vol de token, et révoquait toutes les sessions. Le rate limiting par IP ne protégeait pas contre une attaque répartie.
- **Décision** :
  - Côté client, les rafraîchissements sont sérialisés entre onglets (Web Locks API).
  - Côté serveur, un token remplacé reste accepté pendant 30 s si son remplaçant est toujours actif. Un rafraîchissement concurrent qui perd la course (conflit `xmin`) est rejoué une fois.
  - Le lockout d'ASP.NET Identity est activé : 5 échecs entraînent 15 min de verrouillage. Le message d'échec est le même dans tous les cas, et l'administrateur peut déverrouiller un compte.

### ADR 007 : Validation des entrées
- **Décision** : FluentValidation, exécuté par un filtre MVC global. Les longueurs maximales proviennent de `FieldLimits` (domaine), également utilisé par la configuration EF : la validation et le schéma ne peuvent pas diverger. Les textes produits par le système (résumé IA) sont tronqués à la taille de leur colonne.

### ADR 008 : Règles de déroulement du stage
- **Un seul dossier engagé par élève** : contrôlé à la proposition, à la soumission et à la validation. Les brouillons, dossiers refusés et stages terminés ne comptent pas.
- **Période** : la date de début ne peut pas être passée (dans le fuseau de l'école) ; la durée est comprise entre `Internships:MinDurationWeeks` (8) et `MaxDurationWeeks` (52) ; si le stage répond à une offre, sa durée doit correspondre à celle de l'offre à `OfferDurationToleranceWeeks` (2) près. Ces règles sont revérifiées à la soumission d'un brouillon ancien.
- **Rapports** : uniquement pendant le stage (en cours ou en soutenance), pour une semaine comprise dans la période et déjà commencée ; modifiables par l'élève tant qu'ils ne sont pas validés. Modifier un rapport efface son résumé IA, devenu obsolète.

### ADR 009 : Notation par le jury
- Chaque membre interne du jury dépose une évaluation (note et appréciation), révisable jusqu'à l'arrêt de la note finale. L'élève ne voit jamais les évaluations individuelles.
- La note finale est arrêtée par le président du jury, ou par le responsable de filière ou l'administrateur. La moyenne des évaluations est proposée par défaut.
- Une note arrêtée n'est plus modifiée par une nouvelle saisie : la **correction** est un acte distinct, réservé au responsable ou à l'administrateur, avec un motif obligatoire. L'ancienne et la nouvelle valeur sont conservées par le journal d'audit.

### ADR 010 : Signature de l'entreprise et documents déposés
- **Lien de signature** : un jeton aléatoire de 256 bits est envoyé au tuteur entreprise. Seul son hash SHA-256 est stocké, comparé en temps constant ; il expire au bout de 14 jours, sert une seule fois, et un renvoi invalide le précédent. La page publique n'affiche que le strict nécessaire et elle est soumise à une limite de débit.
- **Signature papier** : l'école ne peut l'enregistrer qu'après le dépôt du scan signé, qui fait office de preuve.
- **Fichiers** : PDF uniquement, vérifié par sa signature `%PDF-` (et non par l'extension) ; 20 Mo maximum ; nom d'affichage assaini ; stockage sous un nom aléatoire, à l'abri de tout parcours de répertoire (`IFileStorage`, implémentation disque sur volume Docker). Les fichiers sont téléchargés en `attachment`. Un nouveau dépôt du même type remplace le précédent et supprime l'ancien fichier.
- **Clés DataProtection** persistées (`DataProtection:KeysPath`) : sans cela, les liens de réinitialisation de mot de passe deviendraient invalides à chaque redémarrage du conteneur.
- **Identifiants** : les `Id` sont générés par le domaine. Ils sont donc déclarés `ValueGeneratedNever`, faute de quoi EF prendrait une entité ajoutée via une navigation pour une ligne existante.

### ADR 011 : Données héritées et pilotage
- Les règles de gestion ne réécrivent jamais les données existantes de façon automatique. Un **rapport d'anomalies** (`GET /api/v1/anomalies`) les détecte et indique la correction à apporter ; c'est un humain qui décide, notamment via l'**annulation administrative** (statut `Annule`, motif obligatoire, interdite une fois la soutenance planifiée).
- L'**année universitaire** est persistée (`Internships.AcademicYear`), calculée à partir de la date de début : l'année commence le 1er septembre. Elle sert aux filtres, au bilan annuel et à l'export. La **promotion** est l'année de sortie de l'élève (`AspNetUsers.GraduationYear`).
- L'export CSV utilise le séparateur `;` et un BOM UTF-8 (pour Excel en français). Les cellules commençant par `= + - @` sont neutralisées (injection CSV).

### ADR 012 : Intégrité de la convention et signature qualifiée
- **Aujourd'hui** : chaque partie signe par une attestation tracée (compte authentifié, lien à usage unique ou scan déposé). À la dernière signature, le PDF est figé et son empreinte SHA-256 est publiée. Cela garantit l'intégrité du document (toute modification est détectable), mais **pas** la valeur d'une signature électronique qualifiée au sens de la loi 43-20 (Maroc) ou du règlement eIDAS.
- **Pour une signature qualifiée**, il faut un prestataire de services de confiance (Barid eSign, Yousign, DocuSign…), un contrat, des clés d'API et une décision juridique de l'établissement. Le point d'intégration est l'exemplaire figé : envoi du PDF figé au prestataire, réception par webhook du PDF signé, puis remplacement de l'exemplaire et de son empreinte. Cette intégration n'est pas implémentée : elle ne peut pas être testée sans compte chez un prestataire.
