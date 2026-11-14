using StageFlow.Domain.Common;

namespace StageFlow.Domain.Events;

public record DefenseScheduledEvent(
    Guid DefenseId,
    Guid InternshipId,
    DateTime DefenseDate,
    string Classroom,
    DateTime OccurredOn) : IDomainEvent;

