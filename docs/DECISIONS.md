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
