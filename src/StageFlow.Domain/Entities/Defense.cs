using StageFlow.Domain.Common;
using StageFlow.Domain.Events;
using StageFlow.Domain.ValueObjects;

namespace StageFlow.Domain.Entities;

public class Defense : BaseAuditableEntity
{
    public DateTime ScheduledAt { get; private set; }

    /// <summary>
    /// Fin du créneau (ScheduledAt + durée), persistée pour permettre à PostgreSQL d'interdire
    /// physiquement deux soutenances qui se chevauchent dans la même salle (contrainte d'exclusion).
    /// </summary>
    public DateTime EndsAt { get; private set; }

    public int DurationMinutes { get; private set; } = 45;
    public string Classroom { get; private set; } = string.Empty;
    public decimal? FinalGrade { get; private set; }
    public string? Comments { get; private set; }

    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public ICollection<DefenseJuryMember> JuryMembers { get; private set; } = new List<DefenseJuryMember>();

    private Defense() { }

    public Defense(Guid internshipId, DateTime scheduledAt, string classroom, int durationMinutes = 45)
    {
        InternshipId = internshipId;
        SetSlot(scheduledAt, classroom, durationMinutes);

        AddDomainEvent(new DefenseScheduledEvent(Id, internshipId, scheduledAt, Classroom, DateTime.UtcNow));
    }

    public void Reschedule(DateTime scheduledAt, string classroom, int durationMinutes)
    {
        SetSlot(scheduledAt, classroom, durationMinutes);
        AddDomainEvent(new DefenseScheduledEvent(Id, InternshipId, scheduledAt, Classroom, DateTime.UtcNow));
    }

    private void SetSlot(DateTime scheduledAt, string classroom, int durationMinutes)
    {
        if (classroom is null) throw new ArgumentNullException(nameof(classroom));

        ScheduledAt = scheduledAt;
        Classroom = classroom.Trim();
        DurationMinutes = durationMinutes > 0 ? durationMinutes : 45;
        EndsAt = ScheduledAt.AddMinutes(DurationMinutes);
    }

    /// <summary>
    /// Enregistre la note finale. Une note ne peut être saisie qu'une fois la soutenance commencée.
    /// </summary>
    public Result RecordGrade(decimal grade, string? comments, DateTime utcNow)
    {
        if (grade < 0 || grade > 20)
            return Result.Failure("La note finale doit être comprise entre 0 et 20.");

        if (decimal.Round(grade, 2) != grade)
            return Result.Failure("La note est limitée à deux décimales.");

        if (utcNow < ScheduledAt)
            return Result.Failure("La note ne peut être saisie qu'après le début de la soutenance.");

        if (comments is { Length: > FieldLimits.DefenseComments })
            return Result.Failure($"L'appréciation ne peut dépasser {FieldLimits.DefenseComments} caractères.");

        FinalGrade = grade;
        Comments = comments;
        return Result.Success();
    }

    public DateRange GetTimeRange() => new(ScheduledAt, EndsAt);
}
