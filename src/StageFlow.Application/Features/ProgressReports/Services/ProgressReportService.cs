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
}

public class ProgressReportService : IProgressReportService
{
    private readonly IApplicationDbContext _context;

    public ProgressReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProgressReportDto>> GetAllReportsAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken)
    {
        var query = _context.ProgressReports
            .Include(r => r.Comments)
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

        return reports.Select(r => MapToDto(r)).ToList();
    }

    public async Task<List<ProgressReportDto>> GetReportsByInternshipAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        var reports = await _context.ProgressReports
            .Include(r => r.Comments)
            .Where(r => r.InternshipId == internshipId)
            .OrderBy(r => r.WeekNumber)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(r => MapToDto(r)).ToList();
    }

    public async Task<Result<ProgressReportDto>> CreateReportAsync(Guid internshipId, CreateReportCommand command, string studentId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { internshipId }, cancellationToken);
        if (internship == null)
            return Result<ProgressReportDto>.Failure("Stage introuvable.");

        if (internship.StudentId != studentId)
            return Result<ProgressReportDto>.Failure("Seul l'élève ingénieur propriétaire du stage peut déposer un rapport.");

        if (internship.Status != InternshipStatus.EnCours && internship.Status != InternshipStatus.SoutenancePlanifiee && internship.Status != InternshipStatus.Valide)
            return Result<ProgressReportDto>.Failure($"Les rapports ne peuvent être déposés que pour un stage validé ou en cours (Statut : {internship.Status}).");

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
