using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Enums;

namespace StageFlow.Infrastructure.Services.Background;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUpcomingDefensesNotifications(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'exécution du service d'arrière-plan de notifications.");
            }

            // Exécution toutes les 6 heures (ou 1 minute en local si besoin)
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task ProcessUpcomingDefensesNotifications(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var in48Hours = DateTime.UtcNow.AddHours(48);
        var in47Hours = DateTime.UtcNow.AddHours(47);

        // Soutenances prévues dans environ 48 heures
        var upcomingDefenses = await context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .Where(d => d.ScheduledAt >= in47Hours && d.ScheduledAt <= in48Hours)
            .ToListAsync(cancellationToken);

        foreach (var defense in upcomingDefenses)
        {
            var subject = $"[StageFlow ENSA] Rappel : Soutenance de stage dans 48h - {defense.Internship.Title}";
            var body = $@"
                <h3>Rappel de soutenance</h3>
                <p>La soutenance pour le stage <strong>{defense.Internship.Title}</strong> aura lieu le :</p>
                <p><strong>Date & Heure :</strong> {defense.ScheduledAt:dd/MM/yyyy à HH:mm} (Durée : {defense.DurationMinutes} min)</p>
                <p><strong>Salle :</strong> {defense.Classroom}</p>
                <p>Merci de vous présenter 15 minutes avant le début de la séance.</p>";

            // Notification de l'élève ingénieur propriétaire du stage.
            var student = await identityService.GetUserByIdAsync(defense.Internship.StudentId, cancellationToken);
            var recipient = student.IsSuccess && !string.IsNullOrWhiteSpace(student.Value!.Email)
                ? student.Value!.Email
                : defense.Internship.CompanySupervisorEmail;

            await emailSender.SendEmailAsync(recipient, subject, body, cancellationToken);
        }
    }
}

