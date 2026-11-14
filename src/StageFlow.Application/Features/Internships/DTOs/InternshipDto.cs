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
    public string? AcademicSupervisorId { get; set; }
    public string CompanySupervisorName { get; set; } = string.Empty;
    public string CompanySupervisorEmail { get; set; } = string.Empty;
    public string? CompanySupervisorPhone { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid AcademicTrackId { get; set; }
    public string AcademicTrackCode { get; set; } = string.Empty;
    public Guid? InternshipOfferId { get; set; }

    public InternshipDto() { }

    public InternshipDto(
        Guid id,
        string title,
        string subjectDescription,
        DateTime startDate,
        DateTime endDate,
        InternshipStatus status,
        string statusName,
        string? rejectionReason,
        string studentId,
        string? academicSupervisorId,
        string companySupervisorName,
        string companySupervisorEmail,
        string? companySupervisorPhone,
        Guid companyId,
        string companyName,
        Guid academicTrackId,
        string academicTrackCode,
        Guid? internshipOfferId)
    {
        Id = id;
        Title = title;
        SubjectDescription = subjectDescription;
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
        StatusName = statusName;
        RejectionReason = rejectionReason;
        StudentId = studentId;
        AcademicSupervisorId = academicSupervisorId;
        CompanySupervisorName = companySupervisorName;
        CompanySupervisorEmail = companySupervisorEmail;
        CompanySupervisorPhone = companySupervisorPhone;
        CompanyId = companyId;
        CompanyName = companyName;
        AcademicTrackId = academicTrackId;
        AcademicTrackCode = academicTrackCode;
        InternshipOfferId = internshipOfferId;
    }
}

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

public class RejectInternshipCommand
{
    public string RejectionReason { get; set; } = string.Empty;
}
