using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.ProgressReports.DTOs;

public record ProgressReportDto(
    Guid Id,
    Guid InternshipId,
    int WeekNumber,
    string Title,
    string ActivitiesPerformed,
    string SkillsAcquired,
    string DifficultiesEncountered,
    string NextWeekGoals,
    string? AiSummary,
    ReportStatus Status,
    DateTime CreatedAt,
    List<ReportCommentDto> Comments,
    string? InternshipTitle = null,
    string? StudentName = null);

public record ReportCommentDto(
    Guid Id,
    string Content,
    string AuthorId,
    string AuthorFullName,
    DateTime CreatedAt);

public class CreateReportCommand
{
    public Guid? InternshipId { get; set; }
    public int WeekNumber { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string ActivitiesPerformed { get; set; } = string.Empty;
    public string SkillsAcquired { get; set; } = string.Empty;
    public string DifficultiesEncountered { get; set; } = string.Empty;
    public string NextWeekGoals { get; set; } = string.Empty;
}

public class UpdateReportCommand
{
    public string Title { get; set; } = string.Empty;
    public string ActivitiesPerformed { get; set; } = string.Empty;
    public string SkillsAcquired { get; set; } = string.Empty;
    public string DifficultiesEncountered { get; set; } = string.Empty;
    public string NextWeekGoals { get; set; } = string.Empty;
}

public record AddCommentCommand(
    string Content);
