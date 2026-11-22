using InternTrack.Domain.Common;

namespace InternTrack.Domain.Events;

public record InternshipStatusChangedEvent(
    Guid InternshipId,
    Enums.InternshipStatus PreviousStatus,
    Enums.InternshipStatus NewStatus,
    string ChangedByUserId,
    DateTime OccurredOn) : IDomainEvent;

