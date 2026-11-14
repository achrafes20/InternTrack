using Microsoft.Extensions.DependencyInjection;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.AuditLedger.Services;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Application.Features.Dashboard.Services;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Application.Features.InternshipOffers.Services;
using StageFlow.Application.Features.Internships.Services;
using StageFlow.Application.Features.ProgressReports.Services;

namespace StageFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IInternshipOfferService, InternshipOfferService>();
        services.AddScoped<IInternshipService, InternshipService>();
        services.AddScoped<IProgressReportService, ProgressReportService>();
        services.AddScoped<IDefenseService, DefenseService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditLedgerService, AuditLedgerService>();
        services.AddScoped<IConventionService, ConventionService>();
        services.AddScoped<IAccessControlService, AccessControlService>();

        return services;
    }
}


