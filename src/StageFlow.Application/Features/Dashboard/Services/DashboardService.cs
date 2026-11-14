using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Dashboard.DTOs;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken);
}

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public DashboardService(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(
        string userId,
        string role,
        Guid? academicTrackId,
        CancellationToken cancellationToken)
    {
        // Chaque rôle ne voit que son périmètre (anti-fuite d'information).
        var internships = ApplyInternshipScope(
            _context.Internships.AsNoTracking(),
            userId,
            role,
            academicTrackId);

        // Compteurs calculés côté SQL (pas de chargement de toutes les lignes en mémoire).
        var totalInternships = await internships.CountAsync(cancellationToken);
        var pendingValidation = await internships.CountAsync(i => i.Status == InternshipStatus.Propose, cancellationToken);
        var inProgress = await internships.CountAsync(i => i.Status == InternshipStatus.EnCours, cancellationToken);
        var completed = await internships.CountAsync(i => i.Status == InternshipStatus.Termine, cancellationToken);

        // Répartition par statut (GROUP BY SQL).
        var statusRaw = await internships
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusDistribution = statusRaw
            .Select(g => new StatusCountDto(g.Status.ToString(), g.Count))
            .OrderByDescending(s => s.Count)
            .ToList();

        // Top 5 des entreprises d'accueil (GROUP BY + ORDER BY + TAKE SQL).
        var topCompaniesRaw = await internships
            .GroupBy(i => i.Company.Name)
            .Select(g => new { CompanyName = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topCompanies = topCompaniesRaw
            .Select(g => new CompanyCountDto(g.CompanyName, g.Count))
            .ToList();

        // Stages par filière (GROUP BY SQL).
        var byTrackRaw = await internships
            .GroupBy(i => new { i.AcademicTrack.Code, i.AcademicTrack.Name })
            .Select(g => new { g.Key.Code, g.Key.Name, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byTrack = byTrackRaw
            .Select(g => new TrackCountDto(g.Code, g.Name, g.Count))
            .OrderByDescending(g => g.Count)
            .ToList();

        // Soutenances à venir (14 prochains jours), filtrées selon le périmètre du rôle.
        var now = DateTime.UtcNow;
        var upcomingLimit = now.AddDays(14);
        var defenses = ApplyDefenseScope(
            _context.Defenses.AsNoTracking(),
            userId,
            role,
            academicTrackId);

        var upcomingDefenses = await defenses
            .Where(d => d.ScheduledAt >= now && d.ScheduledAt <= upcomingLimit)
            .OrderBy(d => d.ScheduledAt)
            .Take(5)
            .Select(d => new
            {
                d.Id,
                d.Internship.StudentId,
                d.Internship.Title,
                d.ScheduledAt,
                d.Classroom
            })
            .ToListAsync(cancellationToken);

        var upcomingDtos = new List<UpcomingDefenseDto>(upcomingDefenses.Count);
        foreach (var defense in upcomingDefenses)
        {
            var student = await _identityService.GetUserByIdAsync(defense.StudentId, cancellationToken);
            var studentName = student.IsSuccess ? student.Value!.FullName : defense.StudentId;
            upcomingDtos.Add(new UpcomingDefenseDto(
                defense.Id,
                studentName,
                defense.Title,
                defense.ScheduledAt,
                defense.Classroom));
        }

        return new DashboardStatsDto(
            totalInternships,
            pendingValidation,
            inProgress,
            completed,
            statusDistribution,
            topCompanies,
            byTrack,
            upcomingDtos);
    }

    private static IQueryable<Internship> ApplyInternshipScope(
        IQueryable<Internship> query,
        string userId,
        string role,
        Guid? academicTrackId)
    {
        return role switch
        {
            UserRole.Etudiant => query.Where(i => i.StudentId == userId),
            UserRole.Encadrant => query.Where(i => i.AcademicSupervisorId == userId),
            UserRole.ResponsableFiliere => academicTrackId.HasValue
                ? query.Where(i => i.AcademicTrackId == academicTrackId.Value)
                : query.Where(_ => false),
            UserRole.Admin => query,
            _ => query.Where(_ => false)
        };
    }

    private static IQueryable<Defense> ApplyDefenseScope(
        IQueryable<Defense> query,
        string userId,
        string role,
        Guid? academicTrackId)
    {
        return role switch
        {
            UserRole.Etudiant => query.Where(d => d.Internship.StudentId == userId),
            UserRole.Encadrant => query.Where(d =>
                d.Internship.AcademicSupervisorId == userId ||
                d.JuryMembers.Any(m => m.TeacherId == userId)),
            UserRole.ResponsableFiliere => academicTrackId.HasValue
                ? query.Where(d => d.Internship.AcademicTrackId == academicTrackId.Value)
                : query.Where(_ => false),
            UserRole.Admin => query,
            _ => query.Where(_ => false)
        };
    }
}
