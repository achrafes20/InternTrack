namespace InternTrack.Application.Features.Dashboard.DTOs;

public record DashboardStatsDto(
    int TotalInternships,
    int PendingValidation,
    int InProgress,
    int Completed,
    List<StatusCountDto> StatusDistribution,
    List<CompanyCountDto> TopCompanies,
    List<TrackCountDto> InternshipsByTrack,
    List<UpcomingDefenseDto> UpcomingDefenses,
    string? AcademicYear = null,
    List<YearlyStatDto>? YearlyStats = null);

/// <summary>Bilan d'une année universitaire (sur le périmètre de l'utilisateur).</summary>
public record YearlyStatDto(
    string AcademicYear,
    int Total,
    int InProgress,
    int Completed,
    int Cancelled,
    decimal? AverageFinalGrade);

public record StatusCountDto(string Status, int Count);
public record CompanyCountDto(string CompanyName, int Count);
public record TrackCountDto(string TrackCode, string TrackName, int Count);
public record UpcomingDefenseDto(Guid Id, string StudentName, string InternshipTitle, DateTime ScheduledAt, string Classroom);

