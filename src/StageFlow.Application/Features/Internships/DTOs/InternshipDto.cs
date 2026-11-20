using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Internships.DTOs;

public class InternshipDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SubjectDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public InternshipStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public string? AcademicSupervisorId { get; set; }
    public string? AcademicSupervisorName { get; set; }
    public string CompanySupervisorName { get; set; } = string.Empty;
    public string CompanySupervisorEmail { get; set; } = string.Empty;
    public string? CompanySupervisorPhone { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid AcademicTrackId { get; set; }
    public string AcademicTrackCode { get; set; } = string.Empty;
    public Guid? InternshipOfferId { get; set; }
    public ConventionStatusDto? Convention { get; set; }
}

public record ConventionStatusDto(
    string ConventionNumber,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    DateTime? SignedAt);

public class ProposeInternshipCommand
{
    public string Title { get; set; } = string.Empty;
    public string SubjectDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AcademicTrackId { get; set; }
    public string CompanySupervisorName { get; set; } = string.Empty;
    public string CompanySupervisorEmail { get; set; } = string.Empty;
    public string? CompanySupervisorPhone { get; set; }
    public Guid? InternshipOfferId { get; set; }
}

public class UpdateInternshipDraftCommand
{
    public string Title { get; set; } = string.Empty;
    public string SubjectDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanySupervisorName { get; set; } = string.Empty;
    public string CompanySupervisorEmail { get; set; } = string.Empty;
    public string? CompanySupervisorPhone { get; set; }
}

public class RejectInternshipCommand
{
    public string RejectionReason { get; set; } = string.Empty;
}
