using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Common.Security;

/// <summary>
/// Centralise les règles d'accès aux ressources métier (anti-IDOR).
/// Les contrôleurs ne doivent jamais se contenter de vérifier qu'un utilisateur est authentifié :
/// ils doivent vérifier qu'il a bien le droit d'accéder à LA ressource demandée.
/// </summary>
public interface IAccessControlService
{
    string? UserId { get; }
    string Role { get; }
    Guid? AcademicTrackId { get; }
    bool IsAdmin { get; }
    bool IsDepartmentHead { get; }
    bool IsSupervisor { get; }
    bool IsStudent { get; }

    Task<bool> CanAccessInternshipAsync(Guid internshipId, CancellationToken cancellationToken = default);
    Task<bool> CanManageInternshipAsync(Guid internshipId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessReportAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<bool> CanSuperviseReportAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessDefenseAsync(Guid defenseId, CancellationToken cancellationToken = default);
    Task<bool> CanManageDefenseAsync(Guid defenseId, CancellationToken cancellationToken = default);

    /// <summary>Encadrants et responsables commentent ; l'élève auteur peut répondre sur ses propres rapports.</summary>
    Task<bool> CanCommentOnReportAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Documents du stage : personnes ayant accès au dossier, plus les membres du jury de sa soutenance.</summary>
    Task<bool> CanViewInternshipDocumentsAsync(Guid internshipId, CancellationToken cancellationToken = default);
}

public class AccessControlService : IAccessControlService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AccessControlService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public string? UserId => _currentUser.UserId;
    public string Role => _currentUser.UserRole ?? string.Empty;
    public Guid? AcademicTrackId => Guid.TryParse(_currentUser.AcademicTrackId, out var trackId) ? trackId : null;
    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsDepartmentHead => Role == UserRole.ResponsableFiliere;
    public bool IsSupervisor => Role == UserRole.Encadrant;
    public bool IsStudent => Role == UserRole.Etudiant;

    public async Task<bool> CanAccessInternshipAsync(Guid internshipId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated) return false;

        var info = await LoadInternshipAccessInfoAsync(internshipId, cancellationToken);
        return info is not null && CanAccess(info);
    }

    public async Task<bool> CanManageInternshipAsync(Guid internshipId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated || IsStudent || IsSupervisor) return false;
        if (IsAdmin) return true;

        var info = await LoadInternshipAccessInfoAsync(internshipId, cancellationToken);
        return info is not null && IsDepartmentHead && IsSameTrack(info.AcademicTrackId);
    }

    public async Task<bool> CanAccessReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated) return false;

        var info = await LoadReportAccessInfoAsync(reportId, cancellationToken);
        return info is not null && CanAccess(info);
    }

    public async Task<bool> CanSuperviseReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated || IsStudent) return false;
        if (IsAdmin) return true;

        var info = await LoadReportAccessInfoAsync(reportId, cancellationToken);
        if (info is null) return false;

        if (IsSupervisor) return info.AcademicSupervisorId == UserId;
        return IsDepartmentHead && IsSameTrack(info.AcademicTrackId);
    }

    public async Task<bool> CanAccessDefenseAsync(Guid defenseId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated) return false;
        if (IsAdmin) return true;

        var info = await LoadDefenseAccessInfoAsync(defenseId, cancellationToken);
        if (info is null) return false;

        if (IsStudent) return info.StudentId == UserId;
        if (IsDepartmentHead) return IsSameTrack(info.AcademicTrackId);

        // Un encadrant accède à la soutenance s'il encadre le stage ou s'il est membre du jury.
        if (IsSupervisor)
        {
            return info.AcademicSupervisorId == UserId ||
                   await _context.Defenses
                       .AsNoTracking()
                       .AnyAsync(d => d.Id == defenseId && d.JuryMembers.Any(m => m.TeacherId == UserId), cancellationToken);
        }

        return false;
    }

    public async Task<bool> CanManageDefenseAsync(Guid defenseId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated || IsStudent || IsSupervisor) return false;
        if (IsAdmin) return true;

        var info = await LoadDefenseAccessInfoAsync(defenseId, cancellationToken);
        return info is not null && IsDepartmentHead && IsSameTrack(info.AcademicTrackId);
    }

    public async Task<bool> CanCommentOnReportAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        if (IsStudent)
            return await CanAccessReportAsync(reportId, cancellationToken);

        return await CanSuperviseReportAsync(reportId, cancellationToken);
    }

    public async Task<bool> CanViewInternshipDocumentsAsync(Guid internshipId, CancellationToken cancellationToken = default)
    {
        if (await CanAccessInternshipAsync(internshipId, cancellationToken))
            return true;

        if (!IsAuthenticated || !IsSupervisor && !IsDepartmentHead)
            return false;

        return await _context.Defenses
            .AsNoTracking()
            .AnyAsync(d => d.InternshipId == internshipId && d.JuryMembers.Any(m => m.TeacherId == UserId), cancellationToken);
    }

    private bool IsAuthenticated => _currentUser.IsAuthenticated && !string.IsNullOrEmpty(UserId);

    private bool CanAccess(InternshipAccessInfo info)
    {
        if (IsAdmin) return true;
        if (IsStudent) return info.StudentId == UserId;
        if (IsSupervisor) return info.AcademicSupervisorId == UserId;
        if (IsDepartmentHead) return IsSameTrack(info.AcademicTrackId);
        return false;
    }

    private bool IsSameTrack(Guid academicTrackId) => AcademicTrackId.HasValue && AcademicTrackId.Value == academicTrackId;

    private Task<InternshipAccessInfo?> LoadInternshipAccessInfoAsync(Guid internshipId, CancellationToken cancellationToken)
        => _context.Internships
            .AsNoTracking()
            .Where(i => i.Id == internshipId)
            .Select(i => new InternshipAccessInfo(i.StudentId, i.AcademicSupervisorId, i.AcademicTrackId))
            .FirstOrDefaultAsync(cancellationToken);

    private Task<InternshipAccessInfo?> LoadReportAccessInfoAsync(Guid reportId, CancellationToken cancellationToken)
        => _context.ProgressReports
            .AsNoTracking()
            .Where(r => r.Id == reportId)
            .Select(r => new InternshipAccessInfo(
                r.Internship.StudentId,
                r.Internship.AcademicSupervisorId,
                r.Internship.AcademicTrackId))
            .FirstOrDefaultAsync(cancellationToken);

    private Task<InternshipAccessInfo?> LoadDefenseAccessInfoAsync(Guid defenseId, CancellationToken cancellationToken)
        => _context.Defenses
            .AsNoTracking()
            .Where(d => d.Id == defenseId)
            .Select(d => new InternshipAccessInfo(
                d.Internship.StudentId,
                d.Internship.AcademicSupervisorId,
                d.Internship.AcademicTrackId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record InternshipAccessInfo(string StudentId, string? AcademicSupervisorId, Guid AcademicTrackId);
}
