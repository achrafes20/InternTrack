using StageFlow.Domain.Common;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;

namespace StageFlow.Domain.Entities;

public class ProgressReport : BaseAuditableEntity
{
    public int WeekNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ActivitiesPerformed { get; private set; } = string.Empty;
    public string SkillsAcquired { get; private set; } = string.Empty;
    public string DifficultiesEncountered { get; private set; } = string.Empty;
    public string NextWeekGoals { get; private set; } = string.Empty;
    public string? AiSummary { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.ARelire;

    // Foreign Key
    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public ICollection<ReportComment> Comments { get; private set; } = new List<ReportComment>();

    private ProgressReport() { }

    public ProgressReport(
        Guid internshipId,
        int weekNumber,
        string title,
        string activitiesPerformed,
        string skillsAcquired,
        string difficultiesEncountered,
        string nextWeekGoals)
    {
        InternshipId = internshipId;
        WeekNumber = weekNumber > 0 ? weekNumber : throw new ArgumentException("Le numéro de semaine doit être > 0.");
        Title = title ?? throw new ArgumentNullException(nameof(title));
        ActivitiesPerformed = activitiesPerformed ?? throw new ArgumentNullException(nameof(activitiesPerformed));
        SkillsAcquired = skillsAcquired ?? string.Empty;
        DifficultiesEncountered = difficultiesEncountered ?? string.Empty;
        NextWeekGoals = nextWeekGoals ?? string.Empty;
        Status = ReportStatus.ARelire;

        AddDomainEvent(new ProgressReportSubmittedEvent(Id, internshipId, weekNumber, DateTime.UtcNow));
    }

    public void Update(
        string title,
        string activitiesPerformed,
        string skillsAcquired,
        string difficultiesEncountered,
        string nextWeekGoals)
    {
        if (Status == ReportStatus.Valide)
            throw new InvalidOperationException("Un rapport déjà validé ne peut plus être modifié.");

        Title = title;
        ActivitiesPerformed = activitiesPerformed;
        SkillsAcquired = skillsAcquired;
        DifficultiesEncountered = difficultiesEncountered;
        NextWeekGoals = nextWeekGoals;
        Status = ReportStatus.ARelire;
    }

    public void SetAiSummary(string summary)
    {
        AiSummary = summary ?? throw new ArgumentNullException(nameof(summary));
    }

    public void MarkAsValidated()
    {
        Status = ReportStatus.Valide;
    }
}

