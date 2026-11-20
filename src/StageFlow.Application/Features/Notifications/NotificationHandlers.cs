using System.Net;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;

namespace StageFlow.Application.Features.Notifications;

/// <summary>
/// Notifie l'élève (et le responsable de filière lors d'une soumission) à chaque changement de statut du dossier.
/// </summary>
public class InternshipStatusChangedNotificationHandler : IDomainEventHandler<InternshipStatusChangedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public InternshipStatusChangedNotificationHandler(IApplicationDbContext context, IIdentityService identityService, IEmailSender emailSender)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task HandleAsync(InternshipStatusChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.AsNoTracking()
            .Where(i => i.Id == domainEvent.InternshipId)
            .Select(i => new { i.Title, i.StudentId, i.AcademicTrackId, i.RejectionReason })
            .FirstOrDefaultAsync(cancellationToken);

        if (internship == null) return;

        var title = WebUtility.HtmlEncode(internship.Title);

        if (domainEvent.NewStatus == InternshipStatus.Propose)
        {
            // Le responsable de la filière doit instruire le dossier.
            var heads = await _identityService.GetUsersInRolesAsync(new[] { UserRole.ResponsableFiliere }, cancellationToken);
            foreach (var head in heads.Where(h => h.IsActive && h.AcademicTrackId == internship.AcademicTrackId))
            {
                await _emailSender.SendEmailAsync(
                    head.Email,
                    $"[StageFlow ENSA] Nouveau dossier à valider : {internship.Title}",
                    $"<p>Le dossier de stage <strong>{title}</strong> a été soumis et attend votre validation.</p>",
                    cancellationToken);
            }
            return;
        }

        var message = domainEvent.NewStatus switch
        {
            InternshipStatus.Valide => "a été <strong>validé</strong>. La convention est disponible et doit être signée.",
            InternshipStatus.Refuse => $"a été <strong>refusé</strong>. Motif : {WebUtility.HtmlEncode(internship.RejectionReason ?? "—")}. Vous pouvez le retravailler puis le soumettre à nouveau.",
            InternshipStatus.EnCours => "est désormais <strong>en cours</strong>. Pensez à déposer vos rapports hebdomadaires.",
            InternshipStatus.SoutenancePlanifiee => "a une <strong>soutenance planifiée</strong>. Consultez le planning des soutenances.",
            InternshipStatus.Termine => "est <strong>terminé</strong>. Félicitations !",
            _ => null
        };

        if (message == null) return;

        var student = await _identityService.GetUserByIdAsync(internship.StudentId, cancellationToken);
        if (student.IsFailure) return;

        await _emailSender.SendEmailAsync(
            student.Value!.Email,
            $"[StageFlow ENSA] Mise à jour de votre dossier : {internship.Title}",
            $"<p>Votre dossier de stage <strong>{title}</strong> {message}</p>",
            cancellationToken);
    }
}

/// <summary>
/// Informe l'élève et les membres internes du jury lors de la planification (ou replanification) d'une soutenance.
/// </summary>
public class DefenseScheduledNotificationHandler : IDomainEventHandler<DefenseScheduledEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public DefenseScheduledNotificationHandler(IApplicationDbContext context, IIdentityService identityService, IEmailSender emailSender)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task HandleAsync(DefenseScheduledEvent domainEvent, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses.AsNoTracking()
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .FirstOrDefaultAsync(d => d.Id == domainEvent.DefenseId, cancellationToken);

        if (defense == null) return;

        var recipientIds = defense.JuryMembers
            .Select(m => m.TeacherId)
            .Where(id => !id.StartsWith("external:", StringComparison.Ordinal))
            .Append(defense.Internship.StudentId)
            .Distinct();

        var subject = $"[StageFlow ENSA] Soutenance planifiée : {defense.Internship.Title}";
        var body = $@"
            <p>La soutenance du stage <strong>{WebUtility.HtmlEncode(defense.Internship.Title)}</strong> est planifiée.</p>
            <p><strong>Date :</strong> {defense.ScheduledAt:dd/MM/yyyy HH:mm} UTC (durée : {defense.DurationMinutes} min)</p>
            <p><strong>Salle :</strong> {WebUtility.HtmlEncode(defense.Classroom)}</p>";

        foreach (var userId in recipientIds)
        {
            var user = await _identityService.GetUserByIdAsync(userId, cancellationToken);
            if (user.IsSuccess)
                await _emailSender.SendEmailAsync(user.Value!.Email, subject, body, cancellationToken);
        }
    }
}

/// <summary>
/// Prévient l'encadrant pédagogique qu'un nouveau rapport hebdomadaire est à relire.
/// </summary>
public class ProgressReportSubmittedNotificationHandler : IDomainEventHandler<ProgressReportSubmittedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public ProgressReportSubmittedNotificationHandler(IApplicationDbContext context, IIdentityService identityService, IEmailSender emailSender)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task HandleAsync(ProgressReportSubmittedEvent domainEvent, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.AsNoTracking()
            .Where(i => i.Id == domainEvent.InternshipId)
            .Select(i => new { i.Title, i.AcademicSupervisorId })
            .FirstOrDefaultAsync(cancellationToken);

        if (internship?.AcademicSupervisorId == null) return;

        var supervisor = await _identityService.GetUserByIdAsync(internship.AcademicSupervisorId, cancellationToken);
        if (supervisor.IsFailure) return;

        await _emailSender.SendEmailAsync(
            supervisor.Value!.Email,
            $"[StageFlow ENSA] Rapport semaine {domainEvent.WeekNumber} à relire",
            $"<p>Un nouveau rapport (semaine {domainEvent.WeekNumber}) a été déposé pour le stage <strong>{WebUtility.HtmlEncode(internship.Title)}</strong>.</p>",
            cancellationToken);
    }
}
