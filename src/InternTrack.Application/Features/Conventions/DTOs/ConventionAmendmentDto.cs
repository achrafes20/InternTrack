using InternTrack.Domain.Entities;

namespace InternTrack.Application.Features.Conventions.DTOs;

public record ConventionAmendmentDto(
    Guid Id,
    int Number,
    string Reason,
    string ChangesSummary,
    DateTime? NewStartDate,
    DateTime? NewEndDate,
    string? NewTitle,
    string? NewSubjectDescription,
    string? NewCompanySupervisorName,
    string? NewCompanySupervisorEmail,
    string? NewCompanySupervisorPhone,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    string? CompanySignerName,
    DateTime CreatedAt,
    DateTime? AppliedAt,
    DateTime? WithdrawnAt,
    string? WithdrawalReason,
    /// <summary>« EnAttente », « Applique » ou « Retire ».</summary>
    string Status);

/// <summary>
/// Demande d'avenant. Seuls les champs renseignés sont modifiés ; au moins un doit l'être.
/// </summary>
public class CreateConventionAmendmentCommand
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewEndDate { get; set; }
    public string? NewTitle { get; set; }
    public string? NewSubjectDescription { get; set; }
    public string? NewCompanySupervisorName { get; set; }
    public string? NewCompanySupervisorEmail { get; set; }
    public string? NewCompanySupervisorPhone { get; set; }
}

public record SignConventionAmendmentCommand(ConventionParty Party, string? SignerName);

public record WithdrawConventionAmendmentCommand(string Reason);
