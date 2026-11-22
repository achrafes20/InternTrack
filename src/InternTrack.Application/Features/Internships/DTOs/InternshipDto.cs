using InternTrack.Domain.Enums;

namespace InternTrack.Application.Features.Internships.DTOs;

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
    public string? CancellationReason { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
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
    public List<InternshipDocumentDto> Documents { get; set; } = new();
    public int TotalWeeks { get; set; }
}

public record ConventionStatusDto(
    string ConventionNumber,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    DateTime? SignedAt,
    string? CompanySignerName = null,
    DateTime? CompanySigningLinkExpiresAt = null,
    DateTime? TerminatedAt = null,
    string? TerminationReason = null);

public record InternshipDocumentDto(
    Guid Id,
    string Kind,
    string FileName,
    long SizeBytes,
    DateTime UploadedAt);

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

public class CancelInternshipCommand
{
    public string Reason { get; set; } = string.Empty;
}
