using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.AuditLedger.Services;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Application.Features.Dashboard.Services;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Application.Features.Documents;
using StageFlow.Application.Features.Anomalies;
using StageFlow.Application.Features.InternshipOffers.Services;
using StageFlow.Application.Features.Internships.Services;
using StageFlow.Application.Features.Notifications;
using StageFlow.Application.Features.ProgressReports.Services;
using StageFlow.Domain.Events;

namespace StageFlow.Application;

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
