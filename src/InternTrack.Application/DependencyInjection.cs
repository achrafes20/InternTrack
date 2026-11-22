using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Common.Security;
using InternTrack.Application.Features.AuditLedger.Services;
using InternTrack.Application.Features.Conventions.Services;
using InternTrack.Application.Features.Dashboard.Services;
using InternTrack.Application.Features.Defenses.Services;
using InternTrack.Application.Features.Documents;
using InternTrack.Application.Features.Anomalies;
using InternTrack.Application.Features.InternshipOffers.Services;
using InternTrack.Application.Features.Internships.Services;
using InternTrack.Application.Features.Notifications;
using InternTrack.Application.Features.ProgressReports.Services;
using InternTrack.Domain.Events;

namespace InternTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInternshipOfferService, InternshipOfferService>();
        services.AddScoped<IInternshipService, InternshipService>();
        services.AddScoped<IProgressReportService, ProgressReportService>();
        services.AddScoped<IDefenseService, DefenseService>();
        services.AddScoped<ITutorEvaluationService, TutorEvaluationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditLedgerService, AuditLedgerService>();
        services.AddScoped<IConventionService, ConventionService>();
        services.AddScoped<IConventionAmendmentService, ConventionAmendmentService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAnomalyService, AnomalyService>();

        // Validateurs d'entrée (FluentValidation), exécutés par un filtre MVC dans l'API.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Réactions aux événements de domaine (notifications).
        services.AddScoped<IDomainEventHandler<InternshipStatusChangedEvent>, InternshipStatusChangedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<DefenseScheduledEvent>, DefenseScheduledNotificationHandler>();
        services.AddScoped<IDomainEventHandler<ProgressReportSubmittedEvent>, ProgressReportSubmittedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<ConventionCompanySignedEvent>, ConventionCompanySignedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<ConventionFullySignedEvent>, ConventionFullySignedHandler>();
        services.AddScoped<IDomainEventHandler<ReportCommentAddedEvent>, ReportCommentAddedNotificationHandler>();

        return services;
    }
}
