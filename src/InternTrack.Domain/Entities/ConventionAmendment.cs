using InternTrack.Domain.Common;

namespace InternTrack.Domain.Entities;

/// <summary>
/// Avenant à une convention signée : prolongation ou modification des dates, changement de tuteur
/// entreprise, reformulation du sujet. Comme la convention, il doit être signé par l'élève, l'entreprise
/// et l'école ; les modifications ne s'appliquent au dossier qu'une fois les trois signatures recueillies.
/// Les champs « New… » nuls signifient « inchangé ».
/// </summary>
public class ConventionAmendment : BaseAuditableEntity
{
    public Guid ConventionId { get; private set; }
    public Convention Convention { get; private set; } = null!;

    /// <summary>Numéro d'ordre de l'avenant sur la convention (1, 2, …).</summary>
    public int Number { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;

    public DateTime? NewStartDate { get; private set; }
    public DateTime? NewEndDate { get; private set; }
    public string? NewTitle { get; private set; }
    public string? NewSubjectDescription { get; private set; }
    public string? NewCompanySupervisorName { get; private set; }
    public string? NewCompanySupervisorEmail { get; private set; }
    public string? NewCompanySupervisorPhone { get; private set; }

    /// <summary>Récapitulatif lisible des valeurs avant/après, figé à la création de l'avenant.</summary>
    public string ChangesSummary { get; private set; } = string.Empty;

    public bool IsSignedByStudent { get; private set; }
    public bool IsSignedByCompany { get; private set; }
    public bool IsSignedBySchool { get; private set; }
    public string? CompanySignerName { get; private set; }

    /// <summary>Date d'application au dossier (toutes les signatures recueillies).</summary>
    public DateTime? AppliedAt { get; private set; }

    /// <summary>Retrait de l'avenant avant application (abandon, erreur, convention résiliée).</summary>
    public DateTime? WithdrawnAt { get; private set; }
    public string? WithdrawalReason { get; private set; }

    public bool IsPending => AppliedAt == null && WithdrawnAt == null;
    public bool IsFullySigned => IsSignedByStudent && IsSignedByCompany && IsSignedBySchool;

    private ConventionAmendment() { }

    public ConventionAmendment(
        Guid conventionId,
        int number,
        string reason,
        string requestedBy,
        DateTime? newStartDate,
        DateTime? newEndDate,
        string? newTitle,
        string? newSubjectDescription,
        string? newCompanySupervisorName,
        string? newCompanySupervisorEmail,
        string? newCompanySupervisorPhone,
        string changesSummary)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Le motif de l'avenant est obligatoire.", nameof(reason));

        ConventionId = conventionId;
        Number = number;
        Reason = FieldLimits.Truncate(reason.Trim(), FieldLimits.RejectionReason);
        RequestedBy = requestedBy ?? throw new ArgumentNullException(nameof(requestedBy));
        NewStartDate = newStartDate;
        NewEndDate = newEndDate;
        NewTitle = newTitle?.Trim();
        NewSubjectDescription = newSubjectDescription?.Trim();
        NewCompanySupervisorName = newCompanySupervisorName?.Trim();
        NewCompanySupervisorEmail = newCompanySupervisorEmail?.Trim();
        NewCompanySupervisorPhone = newCompanySupervisorPhone?.Trim();
        ChangesSummary = FieldLimits.Truncate(changesSummary, FieldLimits.LongDescription);
    }

    public bool IsSignedBy(ConventionParty party) => party switch
    {
        ConventionParty.Student => IsSignedByStudent,
        ConventionParty.Company => IsSignedByCompany,
        ConventionParty.School => IsSignedBySchool,
        _ => false
    };

    public Result Sign(ConventionParty party, string? companySignerName = null)
    {
        if (!IsPending)
            return Result.Failure("Cet avenant n'est plus en attente de signature.");
        if (IsSignedBy(party))
            return Result.Failure("Cette partie a déjà signé l'avenant.");

        switch (party)
        {
            case ConventionParty.Student:
                IsSignedByStudent = true;
                break;
            case ConventionParty.School:
                IsSignedBySchool = true;
                break;
            case ConventionParty.Company:
                if (string.IsNullOrWhiteSpace(companySignerName))
                    return Result.Failure("Le nom du signataire de l'entreprise est obligatoire.");
                IsSignedByCompany = true;
                CompanySignerName = FieldLimits.Truncate(companySignerName.Trim(), FieldLimits.PersonName);
                break;
            default:
                return Result.Failure("Partie signataire inconnue.");
        }

        return Result.Success();
    }

    public Result MarkApplied(DateTime utcNow)
    {
        if (!IsPending)
            return Result.Failure("Cet avenant n'est plus en attente.");
        if (!IsFullySigned)
            return Result.Failure("L'avenant doit être signé par les trois parties avant d'être appliqué.");

        AppliedAt = utcNow;
        return Result.Success();
    }

    public Result Withdraw(string reason, DateTime utcNow)
    {
        if (!IsPending)
            return Result.Failure("Cet avenant n'est plus en attente : il ne peut pas être retiré.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Le motif du retrait est obligatoire.");

        WithdrawnAt = utcNow;
        WithdrawalReason = FieldLimits.Truncate(reason.Trim(), FieldLimits.RejectionReason);
        return Result.Success();
    }
}
