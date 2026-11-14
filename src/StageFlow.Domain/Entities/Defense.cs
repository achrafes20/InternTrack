using StageFlow.Domain.Common;
using StageFlow.Domain.Events;
using StageFlow.Domain.ValueObjects;

namespace StageFlow.Domain.Entities;

public class Defense : BaseAuditableEntity
{
    public DateTime ScheduledAt { get; private set; }
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
        ScheduledAt = scheduledAt;
        Classroom = classroom ?? throw new ArgumentNullException(nameof(classroom));
        DurationMinutes = durationMinutes > 0 ? durationMinutes : 45;

        AddDomainEvent(new DefenseScheduledEvent(Id, internshipId, scheduledAt, classroom, DateTime.UtcNow));
    }

    public void Reschedule(DateTime scheduledAt, string classroom)
    {
        ScheduledAt = scheduledAt;
        Classroom = classroom ?? throw new ArgumentNullException(nameof(classroom));
    }

    public void RecordGrade(decimal grade, string? comments)
    {
        if (grade < 0 || grade > 20)
            throw new ArgumentOutOfRangeException(nameof(grade), "La note finale doit être comprise entre 0 et 20.");

        FinalGrade = grade;
        Comments = comments;
    }

    public DateRange GetTimeRange()
    {
        return new DateRange(ScheduledAt, ScheduledAt.AddMinutes(DurationMinutes));
    }
}

