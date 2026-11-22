using InternTrack.Domain.Common;

namespace InternTrack.Domain.Events;

public record DefenseScheduledEvent(
    Guid DefenseId,
    Guid InternshipId,
    DateTime DefenseDate,
    string Classroom,
    DateTime OccurredOn) : IDomainEvent;

