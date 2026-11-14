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

