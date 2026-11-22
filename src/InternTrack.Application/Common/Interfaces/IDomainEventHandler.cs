using InternTrack.Domain.Common;

namespace InternTrack.Application.Common.Interfaces;

/// <summary>
/// Réagit à un événement de domaine une fois les changements persistés.
/// Un handler ne doit jamais faire échouer la requête d'origine : les erreurs sont journalisées par le dispatcher.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
