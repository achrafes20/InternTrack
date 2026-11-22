using InternTrack.Application.Common.Interfaces;
using InternTrack.Infrastructure.Persistence;
using InternTrack.Infrastructure.Persistence.Outbox;

namespace InternTrack.Infrastructure.Services.Email;

/// <summary>
/// Implémentation de <see cref="IEmailSender"/> utilisée par toute l'application : l'e-mail n'est pas envoyé
/// tout de suite, il est inscrit dans la table EmailOutbox.
/// <list type="bullet">
/// <item>Depuis un handler d'événement de domaine, il rejoint la transaction de la modification métier :
/// soit les deux sont enregistrées, soit aucune.</item>
/// <item>Appelé directement par un service, il est enregistré immédiatement (dans la transaction en cours s'il y en a une).</item>
/// </list>
/// L'envoi réel est fait par <see cref="EmailOutboxProcessor"/>.
/// </summary>
public class OutboxEmailSender : IEmailSender
{
    private readonly InternTrackDbContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxEmailSender(InternTrackDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return;

        _context.EmailOutbox.Add(EmailOutboxMessage.Create(
            toEmail.Trim(), subject, htmlBody, _timeProvider.GetUtcNow().UtcDateTime));

        // Pendant la publication des événements, c'est le DbContext qui enregistre l'outbox avec la transaction.
        if (!_context.IsDispatchingDomainEvents)
            await _context.SaveChangesAsync(cancellationToken);
    }
}
