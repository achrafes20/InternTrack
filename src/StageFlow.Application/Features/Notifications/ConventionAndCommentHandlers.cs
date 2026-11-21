using System.Net;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;

namespace StageFlow.Application.Features.Notifications;

/// <summary>
/// Signature de l'entreprise : l'école (responsables de la filière) et l'élève sont prévenus,
/// l'école pouvant alors apposer sa propre signature puis démarrer le stage.
/// </summary>
public class ConventionCompanySignedNotificationHandler : IDomainEventHandler<ConventionCompanySignedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public ConventionCompanySignedNotificationHandler(IApplicationDbContext context, IIdentityService identityService, IEmailSender emailSender)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task HandleAsync(ConventionCompanySignedEvent domainEvent, CancellationToken cancellationToken)
    {
        var data = await _context.Conventions.AsNoTracking()
            .Where(c => c.Id == domainEvent.ConventionId)
            .Select(c => new { c.ConventionNumber, c.Internship.Title, c.Internship.StudentId, c.Internship.AcademicTrackId, c.Internship.Company.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (data == null) return;

        var subject = $"[StageFlow ENSA] Convention signée par l'entreprise : {data.Title}";
        var body = $@"<p>L'entreprise <strong>{WebUtility.HtmlEncode(data.Name)}</strong> a signé la convention
                      <strong>{WebUtility.HtmlEncode(data.ConventionNumber)}</strong>
                      (signataire : {WebUtility.HtmlEncode(domainEvent.SignerName ?? "—")}).</p>";

        var heads = await _identityService.GetUsersInRolesAsync(new[] { UserRole.ResponsableFiliere }, cancellationToken);
        foreach (var head in heads.Where(h => h.IsActive && h.AcademicTrackId == data.AcademicTrackId))
        {
            await _emailSender.SendEmailAsync(head.Email, subject,
                body + "<p>Il reste, le cas échéant, à apposer la signature de l'école.</p>", cancellationToken);
        }

        var student = await _identityService.GetUserByIdAsync(data.StudentId, cancellationToken);
        if (student.IsSuccess)
            await _emailSender.SendEmailAsync(student.Value!.Email, subject, body, cancellationToken);
    }
}

/// <summary>Dernière signature recueillie : l'exemplaire de référence et son empreinte sont figés immédiatement.</summary>
public class ConventionFullySignedHandler : IDomainEventHandler<ConventionFullySignedEvent>
{
    private readonly IConventionService _conventionService;

    public ConventionFullySignedHandler(IConventionService conventionService) => _conventionService = conventionService;

    public Task HandleAsync(ConventionFullySignedEvent domainEvent, CancellationToken cancellationToken)
        => _conventionService.FreezeSignedDocumentAsync(domainEvent.ConventionId, cancellationToken);
}

/// <summary>
/// Échanges sur un rapport : la réponse de l'élève est signalée à son encadrant,
/// et le commentaire d'un encadrant ou d'un responsable est signalé à l'élève.
/// </summary>
public class ReportCommentAddedNotificationHandler : IDomainEventHandler<ReportCommentAddedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public ReportCommentAddedNotificationHandler(IApplicationDbContext context, IIdentityService identityService, IEmailSender emailSender)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task HandleAsync(ReportCommentAddedEvent domainEvent, CancellationToken cancellationToken)
    {
        var data = await _context.ReportComments.AsNoTracking()
            .Where(c => c.Id == domainEvent.CommentId)
            .Select(c => new
            {
                c.Content,
                c.AuthorFullName,
                c.ProgressReport.WeekNumber,
                c.ProgressReport.Internship.Title,
                c.ProgressReport.Internship.StudentId,
                c.ProgressReport.Internship.AcademicSupervisorId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (data == null) return;

        var authorIsStudent = domainEvent.AuthorId == data.StudentId;
        var recipientId = authorIsStudent ? data.AcademicSupervisorId : data.StudentId;
        if (string.IsNullOrEmpty(recipientId) || recipientId == domainEvent.AuthorId) return;

        var recipient = await _identityService.GetUserByIdAsync(recipientId, cancellationToken);
        if (recipient.IsFailure) return;

        var excerpt = data.Content.Length > 300 ? data.Content[..300] + "…" : data.Content;
        await _emailSender.SendEmailAsync(
            recipient.Value!.Email,
            authorIsStudent
                ? $"[StageFlow ENSA] Réponse de l'élève sur le rapport semaine {data.WeekNumber}"
                : $"[StageFlow ENSA] Nouveau commentaire sur votre rapport semaine {data.WeekNumber}",
            $@"<p><strong>{WebUtility.HtmlEncode(data.AuthorFullName)}</strong> a écrit sur le rapport de la semaine {data.WeekNumber}
                  du stage « {WebUtility.HtmlEncode(data.Title)} » :</p>
               <blockquote>{WebUtility.HtmlEncode(excerpt)}</blockquote>",
            cancellationToken);
    }
}
