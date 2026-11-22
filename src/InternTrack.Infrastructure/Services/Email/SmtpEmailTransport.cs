using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace InternTrack.Infrastructure.Services.Email;

/// <summary>Envoi SMTP (MailKit). Les erreurs remontent au processeur de l'outbox, qui réessaie plus tard.</summary>
public class SmtpEmailTransport : IEmailTransport
{
    private readonly IConfiguration _configuration;

    public SmtpEmailTransport(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.Parse(_configuration["Smtp:Port"] ?? "587");
        var username = _configuration["Smtp:Username"] ?? "";
        var password = _configuration["Smtp:Password"] ?? "";
        var from = _configuration["Smtp:From"] ?? "noreply@interntrack.ensa.ma";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("InternTrack ENSA", from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
