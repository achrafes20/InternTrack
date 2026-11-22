using Microsoft.Extensions.Logging;

namespace InternTrack.Infrastructure.Services.Email;

/// <summary>Transport de développement : l'e-mail est écrit dans les journaux au lieu d'être envoyé.</summary>
public class LoggingEmailTransport : IEmailTransport
{
    private readonly ILogger<LoggingEmailTransport> _logger;

    public LoggingEmailTransport(ILogger<LoggingEmailTransport> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[E-MAIL SIMULÉ] À : {ToEmail} | Objet : {Subject}\n{HtmlBody}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
