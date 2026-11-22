using System.Security.Cryptography;
using InternTrack.Domain.Common;
using InternTrack.Domain.Events;

namespace InternTrack.Domain.Entities;

public enum ConventionParty
{
    Student = 1,
    Company = 2,
    School = 3
}

/// <summary>
/// Convention tripartite (élève, entreprise, école). Chaque partie appose sa signature
/// séparément ; la convention n'est réputée signée qu'une fois les trois signatures recueillies.
/// Les signatures sont des attestations enregistrées par la plateforme (horodatées et tracées
/// dans le journal d'audit), et non des signatures électroniques qualifiées.
/// </summary>
public class Convention : BaseAuditableEntity
{
    public string ConventionNumber { get; private set; } = string.Empty;
    public string VerificationToken { get; private set; } = string.Empty;
    public bool IsSignedByStudent { get; private set; }
    public bool IsSignedByCompany { get; private set; }
    public bool IsSignedBySchool { get; private set; }
    public DateTime? SignedAt { get; private set; }

    /// <summary>Nom de la personne ayant signé pour l'entreprise et date de cette signature.</summary>
    public string? CompanySignerName { get; private set; }
    public DateTime? CompanySignedAt { get; private set; }

    /// <summary>
    /// Lien de signature envoyé à l'entreprise : seul le hash SHA-256 du jeton est conservé
    /// (une fuite de la base ne permet pas de signer à la place de l'entreprise).
    /// </summary>
    public string? CompanySigningTokenHash { get; private set; }
    public DateTime? CompanySigningTokenExpiresAt { get; private set; }

    /// <summary>
    /// Exemplaire de référence figé au moment de la dernière signature, et son empreinte SHA-256.
    /// Toute personne peut recalculer l'empreinte d'un PDF reçu et la comparer à celle publiée
    /// sur la page de vérification : la moindre modification du fichier est détectée.
    /// </summary>
    public string? SignedDocumentStorageKey { get; private set; }
    public string? SignedDocumentSha256 { get; private set; }

    // Foreign Key 1-to-1
    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public bool IsFullySigned => SignedAt.HasValue;

    /// <summary>
    /// Résiliation (rupture anticipée ou annulation du dossier) : la convention ne produit plus d'effet,
    /// ne peut plus être signée et aucun avenant ne peut plus y être rattaché.
    /// </summary>
    public DateTime? TerminatedAt { get; private set; }
    public string? TerminationReason { get; private set; }
    public bool IsTerminated => TerminatedAt.HasValue;

    public ICollection<ConventionAmendment> Amendments { get; private set; } = new List<ConventionAmendment>();

    public Result Terminate(string reason, DateTime utcNow)
    {
        if (IsTerminated)
            return Result.Failure("Cette convention est déjà résiliée.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Le motif de la résiliation est obligatoire.");

        TerminatedAt = utcNow;
        TerminationReason = FieldLimits.Truncate(reason.Trim(), FieldLimits.RejectionReason);
        CompanySigningTokenHash = null;
        CompanySigningTokenExpiresAt = null;

        foreach (var amendment in Amendments.Where(a => a.IsPending))
            amendment.Withdraw("Convention résiliée.", utcNow);

        return Result.Success();
    }

    private Convention() { }

    public Convention(Guid internshipId, string conventionNumber)
    {
        InternshipId = internshipId;
        ConventionNumber = conventionNumber ?? throw new ArgumentNullException(nameof(conventionNumber));
        VerificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    /// <summary>
    /// Point d'entrée unique de création : garantit un format de numéro homogène
    /// (CONV-ENSA-{année}-{6 caractères aléatoires}).
    /// </summary>
    public static Convention CreateFor(Guid internshipId, DateTime utcNow)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
        return new Convention(internshipId, $"CONV-ENSA-{utcNow.Year}-{suffix}");
    }

    public bool IsSignedBy(ConventionParty party) => party switch
    {
        ConventionParty.Student => IsSignedByStudent,
        ConventionParty.Company => IsSignedByCompany,
        ConventionParty.School => IsSignedBySchool,
        _ => false
    };

    public Result Sign(ConventionParty party)
    {
        if (IsTerminated)
            return Result.Failure("Cette convention est résiliée : elle ne peut plus être signée.");

        if (IsSignedBy(party))
            return Result.Failure("Cette partie a déjà signé la convention.");

        switch (party)
        {
            case ConventionParty.Student: IsSignedByStudent = true; break;
            case ConventionParty.Company: IsSignedByCompany = true; break;
            case ConventionParty.School: IsSignedBySchool = true; break;
            default: return Result.Failure("Partie signataire inconnue.");
        }

        CheckCompletion();
        return Result.Success();
    }

    /// <summary>Émet un nouveau lien de signature pour l'entreprise (invalide le précédent). Retourne le jeton brut.</summary>
    public Result<string> IssueCompanySigningToken(DateTime utcNow, TimeSpan validity)
    {
        if (IsSignedByCompany)
            return Result<string>.Failure("L'entreprise a déjà signé cette convention.");
        if (IsTerminated)
            return Result<string>.Failure("Cette convention est résiliée.");

        var rawToken = SecureToken.Generate();
        CompanySigningTokenHash = SecureToken.Hash(rawToken);
        CompanySigningTokenExpiresAt = utcNow.Add(validity);
        return Result<string>.Success(rawToken);
    }

    public bool IsCompanySigningTokenValid(string rawToken, DateTime utcNow)
        => CompanySigningTokenHash != null
           && CompanySigningTokenExpiresAt > utcNow
           && SecureToken.Matches(CompanySigningTokenHash, rawToken);

    /// <summary>Signature par l'entreprise via le lien reçu par e-mail (jeton à usage unique).</summary>
    public Result SignByCompanyWithToken(string rawToken, string signerName, DateTime utcNow)
    {
        if (IsSignedByCompany)
            return Result.Failure("L'entreprise a déjà signé cette convention.");

        if (!IsCompanySigningTokenValid(rawToken, utcNow))
            return Result.Failure("Ce lien de signature est invalide ou a expiré. Demandez un nouveau lien à l'école.");

        return RecordCompanySignature(signerName, utcNow);
    }

    /// <summary>
    /// Enregistre la signature de l'entreprise : soit via le lien, soit par l'école à réception de
    /// l'exemplaire papier signé (dont le scan doit alors être déposé, contrôle fait par le service).
    /// </summary>
    public Result RecordCompanySignature(string signerName, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(signerName))
            return Result.Failure("Le nom du signataire de l'entreprise est obligatoire.");

        var result = Sign(ConventionParty.Company);
        if (result.IsFailure)
            return result;

        CompanySignerName = FieldLimits.Truncate(signerName.Trim(), FieldLimits.PersonName);
        CompanySignedAt = utcNow;
        CompanySigningTokenHash = null;
        CompanySigningTokenExpiresAt = null;
        AddDomainEvent(new ConventionCompanySignedEvent(Id, InternshipId, CompanySignerName, DateTime.UtcNow));
        return Result.Success();
    }

    /// <summary>Fige l'exemplaire signé (une seule fois) : il ne sera plus jamais régénéré.</summary>
    public Result FreezeSignedDocument(string storageKey, string sha256)
    {
        if (!IsFullySigned)
            return Result.Failure("Seule une convention signée par les trois parties peut être figée.");
        if (SignedDocumentStorageKey != null)
            return Result.Failure("L'exemplaire signé est déjà figé.");

        SignedDocumentStorageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        SignedDocumentSha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
        return Result.Success();
    }

    /// <summary>Hash permettant de retrouver la convention correspondant à un lien de signature.</summary>
    public static string HashSigningToken(string rawToken) => SecureToken.Hash(rawToken);

    public void SignByStudent() => Sign(ConventionParty.Student);
    public void SignByCompany() => Sign(ConventionParty.Company);
    public void SignBySchool() => Sign(ConventionParty.School);

    private void CheckCompletion()
    {
        if (IsSignedByStudent && IsSignedByCompany && IsSignedBySchool && SignedAt == null)
        {
            SignedAt = DateTime.UtcNow;
            AddDomainEvent(new ConventionFullySignedEvent(Id, InternshipId, SignedAt.Value));
        }
    }
}
