using StageFlow.Domain.Common;

namespace StageFlow.Domain.Events;

public record InternshipStatusChangedEvent(
    Guid InternshipId,
    Enums.InternshipStatus PreviousStatus,
    Enums.InternshipStatus NewStatus,
    string ChangedByUserId,
    DateTime OccurredOn) : IDomainEvent;

