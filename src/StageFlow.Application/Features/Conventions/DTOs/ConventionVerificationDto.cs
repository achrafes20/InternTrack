namespace StageFlow.Application.Features.Conventions.DTOs;

public record ConventionVerificationDto(
    string ConventionNumber,
    string StudentFullName,
    string AcademicTrack,
    string CompanyName,
    DateTime StartDate,
    DateTime EndDate,
    bool IsAuthentic,
    bool IsFullySigned,
    DateTime? SignedAt);

