namespace InternTrack.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

