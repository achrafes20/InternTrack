using Microsoft.Extensions.Logging;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Infrastructure.Services.Email;

public class FakeEmailSender : IEmailSender
{
    private readonly ILogger<FakeEmailSender> _logger;

    public FakeEmailSender(ILogger<FakeEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        _logger.LogInformation("================== [FAKE EMAIL SENDER] ==================");
        _logger.LogInformation("To: {ToEmail}", toEmail);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Body: {HtmlBody}", htmlBody);
        _logger.LogInformation("=========================================================");

        return Task.CompletedTask;
    }
}

