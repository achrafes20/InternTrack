using StageFlow.Domain.Entities;

namespace StageFlow.Application.Features.Conventions.DTOs;

public record ConventionVerificationDto(
    string ConventionNumber,
    string StudentFullName,
    string AcademicTrack,
    string CompanyName,
    DateTime StartDate,
    DateTime EndDate,
    bool IsAuthentic,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    bool IsFullySigned,
    DateTime? SignedAt);

public record SignConventionCommand(ConventionParty Party);
