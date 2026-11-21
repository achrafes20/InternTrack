using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.ProgressReports.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.ProgressReports.Services;

public interface IProgressReportService
{
    Task<List<ProgressReportDto>> GetAllReportsAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken);
    Task<List<ProgressReportDto>> GetReportsByInternshipAsync(Guid internshipId, CancellationToken cancellationToken);
    Task<Result<ProgressReportDto>> CreateReportAsync(Guid internshipId, CreateReportCommand command, string studentId, CancellationToken cancellationToken);
    Task<Result<ReportCommentDto>> AddCommentAsync(Guid reportId, string content, string authorId, string authorFullName, CancellationToken cancellationToken);
    Task<Result> ValidateReportAsync(Guid reportId, string supervisorId, CancellationToken cancellationToken);
    Task<Result<ProgressReportDto>> UpdateReportAsync(Guid reportId, UpdateReportCommand command, string studentId, CancellationToken cancellationToken);
}

public class ProgressReportService : IProgressReportService
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly ITimeZoneService _timeZone;

    public ProgressReportService(IApplicationDbContext context, IIdentityService identityService, ITimeZoneService timeZone)
    {
        _context = context;
        _identityService = identityService;
        _timeZone = timeZone;
    }

    /// <summary>
    /// La semaine doit appartenir à la période du stage et avoir déjà commencé :
    /// on ne rédige pas le rapport d'une semaine future.
    /// </summary>
    private Result ValidateWeek(Internship internship, int weekNumber)
    {
        if (weekNumber < 1 || weekNumber > internship.TotalWeeks)
            return Result.Failure($"Le numéro de semaine doit être compris entre 1 et {internship.TotalWeeks} (durée du stage).");

        var weekStartLocal = _timeZone.ToLocal(internship.WeekStart(weekNumber)).Date;
        if (weekStartLocal > _timeZone.LocalToday)
            return Result.Failure($"La semaine {weekNumber} commence le {weekStartLocal:dd/MM/yyyy} : son rapport ne peut pas encore être déposé.");

        return Result.Success();
    }

    public async Task<Result<ProgressReportDto>> UpdateReportAsync(Guid reportId, UpdateReportCommand command, string studentId, CancellationToken cancellationToken)
    {
        var report = await _context.ProgressReports
            .Include(r => r.Internship)
            .Include(r => r.Comments)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
            return Result<ProgressReportDto>.Failure("Rapport introuvable.");

        if (report.Internship.StudentId != studentId)
            return Result<ProgressReportDto>.Failure("Seul l'élève ingénieur auteur du rapport peut le modifier.");

        if (!Internship.RunningStatuses.Contains(report.Internship.Status))
            return Result<ProgressReportDto>.Failure("Le stage est clôturé : ses rapports ne peuvent plus être modifiés.");

        var update = report.Update(command.Title, command.ActivitiesPerformed, command.SkillsAcquired, command.DifficultiesEncountered, command.NextWeekGoals);
        if (update.IsFailure)
            return Result<ProgressReportDto>.Failure(update.Error);

        await _context.SaveChangesAsync(cancellationToken);
        return Result<ProgressReportDto>.Success(MapToDto(report));
    }

    public async Task<List<ProgressReportDto>> GetAllReportsAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken)
    {
        var query = _context.ProgressReports
            .Include(r => r.Comments)
            .Include(r => r.Internship)
            .AsNoTracking()
            .AsQueryable();

        // Périmètre de visibilité selon le rôle (anti-IDOR).
        if (role == UserRole.Etudiant)
        {
            query = query.Where(r => r.Internship.StudentId == userId);
        }
        else if (role == UserRole.Encadrant)
        {
            query = query.Where(r => r.Internship.AcademicSupervisorId == userId);
        }
        else if (role == UserRole.ResponsableFiliere)
        {
            query = academicTrackId.HasValue
                ? query.Where(r => r.Internship.AcademicTrackId == academicTrackId.Value)
                : query.Where(_ => false);
        }
        else if (role != UserRole.Admin)
        {
            query = query.Where(_ => false);
        }

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapWithContextAsync(reports, cancellationToken);
    }

    public async Task<List<ProgressReportDto>> GetReportsByInternshipAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        var reports = await _context.ProgressReports
            .Include(r => r.Comments)
            .Include(r => r.Internship)
            .Where(r => r.InternshipId == internshipId)
            .OrderBy(r => r.WeekNumber)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await MapWithContextAsync(reports, cancellationToken);
    }

    private async Task<List<ProgressReportDto>> MapWithContextAsync(List<ProgressReport> reports, CancellationToken cancellationToken)
    {
        var names = await _identityService.GetUserNamesAsync(
            reports.Select(r => r.Internship.StudentId).Distinct(), cancellationToken);

        return reports
            .Select(r => MapToDto(r) with
            {
                InternshipTitle = r.Internship.Title,
                StudentName = names.GetValueOrDefault(r.Internship.StudentId)
            })
            .ToList();
    }

    public async Task<Result<ProgressReportDto>> CreateReportAsync(Guid internshipId, CreateReportCommand command, string studentId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { internshipId }, cancellationToken);
        if (internship == null)
            return Result<ProgressReportDto>.Failure("Stage introuvable.");

        if (internship.StudentId != studentId)
            return Result<ProgressReportDto>.Failure("Seul l'élève ingénieur propriétaire du stage peut déposer un rapport.");

        if (!Internship.RunningStatuses.Contains(internship.Status))
            return Result<ProgressReportDto>.Failure($"Les rapports ne peuvent être déposés que pendant le stage, une fois celui-ci démarré (Statut : {internship.Status}).");

        var weekCheck = ValidateWeek(internship, command.WeekNumber);
        if (weekCheck.IsFailure)
            return Result<ProgressReportDto>.Failure(weekCheck.Error);

        if (string.IsNullOrWhiteSpace(command.ActivitiesPerformed))
            return Result<ProgressReportDto>.Failure("La description des activités réalisées est obligatoire.");

        var existingReport = await _context.ProgressReports
            .AnyAsync(r => r.InternshipId == internshipId && r.WeekNumber == command.WeekNumber, cancellationToken);

        if (existingReport)
            return Result<ProgressReportDto>.Failure($"Un rapport pour la semaine {command.WeekNumber} existe déjà.");

        var reportTitle = string.IsNullOrWhiteSpace(command.Title)
            ? $"Rapport Semaine {command.WeekNumber}"
            : command.Title;

        var report = new ProgressReport(
            internshipId,
            command.WeekNumber,
            reportTitle,
            command.ActivitiesPerformed,
            command.SkillsAcquired,
            command.DifficultiesEncountered,
            command.NextWeekGoals);

        await _context.ProgressReports.AddAsync(report, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ProgressReportDto>.Success(MapToDto(report));
    }

    public async Task<Result<ReportCommentDto>> AddCommentAsync(Guid reportId, string content, string authorId, string authorFullName, CancellationToken cancellationToken)
    {
        var report = await _context.ProgressReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (report == null)
            return Result<ReportCommentDto>.Failure("Rapport introuvable.");

        if (string.IsNullOrWhiteSpace(content))
            return Result<ReportCommentDto>.Failure("Le contenu du commentaire ne peut pas être vide.");

        var comment = new ReportComment(reportId, authorId, authorFullName, content);
        await _context.ReportComments.AddAsync(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ReportCommentDto>.Success(new ReportCommentDto(comment.Id, comment.Content, comment.AuthorId, comment.AuthorFullName, comment.CreatedAt));
    }

    public async Task<Result> ValidateReportAsync(Guid reportId, string supervisorId, CancellationToken cancellationToken)
    {
        var report = await _context.ProgressReports.FindAsync(new object[] { reportId }, cancellationToken);
        if (report == null)
            return Result.Failure("Rapport introuvable.");

        if (report.Status == ReportStatus.Valide)
            return Result.Failure("Ce rapport est déjà validé.");

        report.MarkAsValidated();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static ProgressReportDto MapToDto(ProgressReport r)
    {
        return new ProgressReportDto(
            r.Id,
            r.InternshipId,
            r.WeekNumber,
            r.Title,
            r.ActivitiesPerformed,
            r.SkillsAcquired,
            r.DifficultiesEncountered,
            r.NextWeekGoals,
            r.AiSummary,
            r.Status,
            r.CreatedAt,
            r.Comments?.Select(c => new ReportCommentDto(c.Id, c.Content, c.AuthorId, c.AuthorFullName, c.CreatedAt)).ToList() ?? new List<ReportCommentDto>()
        );
    }
}
