using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Internships.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Internships.Services;

public interface IInternshipService
{
    Task<List<InternshipDto>> GetAllAsync(string? studentId, string? supervisorId, InternshipStatus? status, Guid? trackId, CancellationToken cancellationToken);
    Task<Result<InternshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<InternshipDto>> ProposeAsync(ProposeInternshipCommand command, string studentId, CancellationToken cancellationToken);
    Task<Result> SubmitAsync(Guid id, string studentId, CancellationToken cancellationToken);
    Task<Result> ApproveAsync(Guid id, string currentUserId, CancellationToken cancellationToken);
    Task<Result> RejectAsync(Guid id, string currentUserId, string rejectionReason, CancellationToken cancellationToken);
    Task<Result> StartInternshipAsync(Guid id, string currentUserId, CancellationToken cancellationToken);
    Task<Result> AssignSupervisorAsync(Guid id, string supervisorId, CancellationToken cancellationToken);
}

public class InternshipService : IInternshipService
{
    private readonly IApplicationDbContext _context;

    public InternshipService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<InternshipDto>> GetAllAsync(string? studentId, string? supervisorId, InternshipStatus? status, Guid? trackId, CancellationToken cancellationToken)
    {
        var query = _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(studentId))
            query = query.Where(i => i.StudentId == studentId);

        if (!string.IsNullOrEmpty(supervisorId))
            query = query.Where(i => i.AcademicSupervisorId == supervisorId);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (trackId.HasValue)
            query = query.Where(i => i.AcademicTrackId == trackId.Value);

        return await query.OrderByDescending(i => i.CreatedAt)
            .Select(i => MapToDto(i))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<InternshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
            .Include(i => i.Defense)
            .Include(i => i.Reports)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (internship == null)
            return Result<InternshipDto>.Failure("Dossier de stage introuvable.");

        return Result<InternshipDto>.Success(MapToDto(internship));
    }

    public async Task<Result<InternshipDto>> ProposeAsync(ProposeInternshipCommand command, string studentId, CancellationToken cancellationToken)
    {
        var company = await _context.Companies.FindAsync(new object[] { command.CompanyId }, cancellationToken);
        if (company == null)
            return Result<InternshipDto>.Failure("L'entreprise spécifiée n'existe pas.");

        var track = await _context.AcademicTracks.FindAsync(new object[] { command.AcademicTrackId }, cancellationToken);
        if (track == null)
            return Result<InternshipDto>.Failure("La filière spécifiée n'existe pas.");

        try
        {
            var internship = new Internship(
                command.Title,
                command.SubjectDescription,
                command.StartDate,
                command.EndDate,
                studentId,
                command.CompanyId,
                command.AcademicTrackId,
                command.CompanySupervisorName,
                command.CompanySupervisorEmail,
                command.CompanySupervisorPhone,
                command.InternshipOfferId);

            await _context.Internships.AddAsync(internship, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<InternshipDto>.Success(MapToDto(internship, company.Name, track.Code));
        }
        catch (ArgumentException ex)
        {
            return Result<InternshipDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> SubmitAsync(Guid id, string studentId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = internship.SubmitForApproval(studentId);
        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ApproveAsync(Guid id, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = internship.Approve(currentUserId);
        if (result.IsFailure) return result;

        // Génération automatique de la convention de stage (idempotent : une seule convention par stage).
        if (!await _context.Conventions.AnyAsync(c => c.InternshipId == internship.Id, cancellationToken))
        {
            var conventionNumber = $"CONV-{DateTime.UtcNow.Year}-{id.ToString().Substring(0, 8).ToUpper()}";
            var convention = new Convention(internship.Id, conventionNumber);
            await _context.Conventions.AddAsync(convention, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid id, string currentUserId, string rejectionReason, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = internship.Reject(currentUserId, rejectionReason);
        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> StartInternshipAsync(Guid id, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = internship.StartInternship(currentUserId);
        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AssignSupervisorAsync(Guid id, string supervisorId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        internship.AssignAcademicSupervisor(supervisorId);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static InternshipDto MapToDto(Internship i, string? companyName = null, string? trackCode = null)
    {
        return new InternshipDto(
            i.Id,
            i.Title,
            i.SubjectDescription,
            i.Period.StartDate,
            i.Period.EndDate,
            i.Status,
            i.Status.ToString(),
            i.RejectionReason,
            i.StudentId,
            i.AcademicSupervisorId,
            i.CompanySupervisorName,
            i.CompanySupervisorEmail,
            i.CompanySupervisorPhone,
            i.CompanyId,
            companyName ?? i.Company?.Name ?? "Inconnue",
            i.AcademicTrackId,
            trackCode ?? i.AcademicTrack?.Code ?? "N/A",
            i.InternshipOfferId);
    }
}

