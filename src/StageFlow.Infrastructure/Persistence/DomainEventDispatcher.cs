using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Common;

namespace StageFlow.Infrastructure.Persistence;

/// <summary>
/// Publie les événements de domaine vers leurs handlers après persistance des changements.
/// Une erreur dans un handler (ex. serveur SMTP indisponible) est journalisée mais ne fait
/// jamais échouer la requête métier déjà validée en base.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler is null) continue;

                try
                {
                    var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                    await (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Échec du traitement de l'événement de domaine {EventType} par {Handler}.",
                        domainEvent.GetType().Name, handler.GetType().Name);
                }
            }
        }
    }
}
