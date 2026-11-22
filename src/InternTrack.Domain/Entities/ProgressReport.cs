using InternTrack.Domain.Common;
using InternTrack.Domain.Enums;
using InternTrack.Domain.Events;

namespace InternTrack.Domain.Entities;

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

    /// <summary>
    /// Modification par l'élève tant que le rapport n'a pas été validé. Le résumé IA, devenu obsolète, est effacé.
    /// </summary>
    public Result Update(
        string title,
        string activitiesPerformed,
        string skillsAcquired,
        string difficultiesEncountered,
        string nextWeekGoals)
    {
        if (Status == ReportStatus.Valide)
            return Result.Failure("Un rapport déjà validé par l'encadrant ne peut plus être modifié.");

        if (string.IsNullOrWhiteSpace(activitiesPerformed))
            return Result.Failure("La description des activités réalisées est obligatoire.");

        Title = string.IsNullOrWhiteSpace(title) ? $"Rapport Semaine {WeekNumber}" : title;
        ActivitiesPerformed = activitiesPerformed;
        SkillsAcquired = skillsAcquired ?? string.Empty;
        DifficultiesEncountered = difficultiesEncountered ?? string.Empty;
        NextWeekGoals = nextWeekGoals ?? string.Empty;
        AiSummary = null;
        Status = ReportStatus.ARelire;
        return Result.Success();
    }

    public void SetAiSummary(string summary)
    {
        if (summary is null) throw new ArgumentNullException(nameof(summary));
        // Texte produit par un service externe : borné à la taille de la colonne.
        AiSummary = FieldLimits.Truncate(summary, FieldLimits.AiSummary);
    }

    public void MarkAsValidated()
    {
        Status = ReportStatus.Valide;
    }
}

