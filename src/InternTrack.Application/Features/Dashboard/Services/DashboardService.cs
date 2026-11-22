using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Common.Models;
using InternTrack.Application.Features.Dashboard.DTOs;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;

namespace InternTrack.Application.Features.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken, string? academicYear = null);

    /// <summary>Export CSV (séparateur « ; », UTF-8 avec BOM : ouvrable directement dans Excel) des stages du périmètre.</summary>
    Task<string> ExportCsvAsync(string userId, string role, Guid? academicTrackId, string? academicYear, CancellationToken cancellationToken);
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
        CancellationToken cancellationToken,
        string? academicYear = null)
    {
        // Chaque rôle ne voit que son périmètre (anti-fuite d'information).
        var scoped = ApplyInternshipScope(
            _context.Internships.AsNoTracking(),
            userId,
            role,
            academicTrackId);

        // Bilan par année, toutes années confondues (le filtre ci-dessous ne s'y applique pas).
        var yearlyRaw = await scoped
            .GroupBy(i => i.AcademicYear)
            .Select(g => new
            {
                Year = g.Key,
                Total = g.Count(),
                InProgress = g.Count(i => i.Status == InternshipStatus.EnCours || i.Status == InternshipStatus.SoutenancePlanifiee),
                Completed = g.Count(i => i.Status == InternshipStatus.Termine),
                Cancelled = g.Count(i => i.Status == InternshipStatus.Annule),
                Average = g.Where(i => i.Defense != null && i.Defense.FinalGrade != null).Average(i => (decimal?)i.Defense!.FinalGrade)
            })
            .ToListAsync(cancellationToken);

        var yearlyStats = yearlyRaw
            .OrderByDescending(y => y.Year)
            .Select(y => new YearlyStatDto(y.Year, y.Total, y.InProgress, y.Completed, y.Cancelled,
                y.Average.HasValue ? decimal.Round(y.Average.Value, 2) : null))
            .ToList();

        var internships = string.IsNullOrWhiteSpace(academicYear)
            ? scoped
            : scoped.Where(i => i.AcademicYear == academicYear);

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
            .Where(d => d.CancelledAt == null && d.ScheduledAt >= now && d.ScheduledAt <= upcomingLimit)
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

        var names = await _identityService.GetUserNamesAsync(
            upcomingDefenses.Select(d => d.StudentId).Distinct(), cancellationToken);

        var upcomingDtos = upcomingDefenses
            .Select(d => new UpcomingDefenseDto(
                d.Id,
                names.GetValueOrDefault(d.StudentId) ?? "Élève ingénieur",
                d.Title,
                d.ScheduledAt,
                d.Classroom))
            .ToList();

        return new DashboardStatsDto(
            totalInternships,
            pendingValidation,
            inProgress,
            completed,
            statusDistribution,
            topCompanies,
            byTrack,
            upcomingDtos,
            academicYear,
            yearlyStats);
    }

    public async Task<string> ExportCsvAsync(string userId, string role, Guid? academicTrackId, string? academicYear, CancellationToken cancellationToken)
    {
        var query = ApplyInternshipScope(_context.Internships.AsNoTracking(), userId, role, academicTrackId);
        if (!string.IsNullOrWhiteSpace(academicYear))
            query = query.Where(i => i.AcademicYear == academicYear);

        var rows = await query
            .OrderBy(i => i.AcademicYear).ThenBy(i => i.AcademicTrack.Code).ThenBy(i => i.StudentId)
            .Select(i => new
            {
                i.AcademicYear,
                i.StudentId,
                Track = i.AcademicTrack.Code,
                Company = i.Company.Name,
                City = i.Company.City,
                i.Title,
                i.Period.StartDate,
                i.Period.EndDate,
                i.Status,
                i.AcademicSupervisorId,
                ConventionSigned = i.Convention != null && i.Convention.SignedAt != null,
                Grade = i.Defense != null ? i.Defense.FinalGrade : null
            })
            .ToListAsync(cancellationToken);

        var students = (await _identityService.GetUsersInRolesAsync(new[] { UserRole.Etudiant }, cancellationToken))
            .ToDictionary(u => u.Id);
        var supervisors = await _identityService.GetUserNamesAsync(
            rows.Where(r => r.AcademicSupervisorId != null).Select(r => r.AcademicSupervisorId!).Distinct(), cancellationToken);

        var csv = new CsvBuilder();
        csv.AddRow("Année universitaire", "Promotion", "Élève", "Code Apogée", "Email", "Filière", "Entreprise", "Ville",
            "Sujet", "Début", "Fin", "Statut", "Encadrant", "Convention signée", "Note finale");

        foreach (var r in rows)
        {
            students.TryGetValue(r.StudentId, out var student);
            csv.AddRow(
                r.AcademicYear,
                student?.GraduationYear?.ToString() ?? "",
                student?.FullName ?? "",
                student?.StudentNumber ?? "",
                student?.Email ?? "",
                r.Track,
                r.Company,
                r.City,
                r.Title,
                r.StartDate.ToString("dd/MM/yyyy"),
                r.EndDate.ToString("dd/MM/yyyy"),
                r.Status.ToString(),
                r.AcademicSupervisorId != null ? supervisors.GetValueOrDefault(r.AcademicSupervisorId) ?? "" : "",
                r.ConventionSigned ? "Oui" : "Non",
                r.Grade?.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) ?? "");
        }

        return csv.ToString();
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
