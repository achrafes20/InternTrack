using System.Security.Cryptography;
using StageFlow.Domain.Common;

namespace StageFlow.Domain.Entities;

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

    // Foreign Key 1-to-1
    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public bool IsFullySigned => SignedAt.HasValue;

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

    public void SignByStudent() => Sign(ConventionParty.Student);
    public void SignByCompany() => Sign(ConventionParty.Company);
    public void SignBySchool() => Sign(ConventionParty.School);

    private void CheckCompletion()
    {
        if (IsSignedByStudent && IsSignedByCompany && IsSignedBySchool && SignedAt == null)
        {
            SignedAt = DateTime.UtcNow;
        }
    }
}
