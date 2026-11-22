namespace InternTrack.Infrastructure.Services.Email;

/// <summary>
/// Envoi réel d'un e-mail (SMTP ou journal en développement). Contrairement à <c>IEmailSender</c>,
/// une erreur est levée : c'est le processeur de l'outbox qui décide de réessayer.
/// </summary>
public interface IEmailTransport
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken);
}
