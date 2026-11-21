using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Infrastructure.Services.Background;

/// <summary>
/// Envoie un rappel 48 h avant chaque soutenance à l'élève et aux membres internes du jury.
/// Chaque exécution traite la fenêtre [dernière exécution + 48 h ; maintenant + 48 h[ : les fenêtres
/// successives sont contiguës et sans recouvrement, aucune soutenance n'est donc oubliée ni notifiée deux fois
/// tant que le service tourne.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    public static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(48);
    public static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeProvider _timeProvider;

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger,
        TimeProvider? timeProvider = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService démarré.");

        var windowStart = _timeProvider.GetUtcNow().UtcDateTime;

        using var timer = new PeriodicTimer(PollingInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            try
            {
                await ProcessWindowAsync(windowStart + ReminderLeadTime, now + ReminderLeadTime, stoppingToken);
                windowStart = now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // La fenêtre n'avance pas : elle sera retraitée au prochain cycle.
                _logger.LogError(ex, "Erreur lors de l'envoi des rappels de soutenance.");
            }
        }
    }

    /// <summary>Envoie les rappels des soutenances programmées dans [from ; to[.</summary>
    public async Task<int> ProcessWindowAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var timeZone = scope.ServiceProvider.GetRequiredService<ITimeZoneService>();

        var upcomingDefenses = await context.Defenses
            .AsNoTracking()
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .Where(d => d.ScheduledAt >= from && d.ScheduledAt < to && d.FinalGrade == null)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var defense in upcomingDefenses)
        {
            var subject = $"[StageFlow ENSA] Rappel : soutenance dans 48h - {defense.Internship.Title}";
            var body = $@"
                <h3>Rappel de soutenance</h3>
                <p>La soutenance du stage <strong>{System.Net.WebUtility.HtmlEncode(defense.Internship.Title)}</strong> aura lieu le :</p>
                <p><strong>Date &amp; heure :</strong> {timeZone.FormatDateTime(defense.ScheduledAt)} (durée : {defense.DurationMinutes} min)</p>
                <p><strong>Salle :</strong> {System.Net.WebUtility.HtmlEncode(defense.Classroom)}</p>
                <p>Merci de vous présenter 15 minutes avant le début de la séance.</p>";

            var recipientIds = defense.JuryMembers
                .Select(m => m.TeacherId)
                .Where(id => !id.StartsWith("external:", StringComparison.Ordinal))
                .Append(defense.Internship.StudentId)
                .Distinct();

            foreach (var userId in recipientIds)
            {
                var user = await identityService.GetUserByIdAsync(userId, cancellationToken);
                if (user.IsFailure || string.IsNullOrWhiteSpace(user.Value!.Email)) continue;

                await emailSender.SendEmailAsync(user.Value.Email, subject, body, cancellationToken);
                sent++;
            }
        }

        return sent;
    }
}
