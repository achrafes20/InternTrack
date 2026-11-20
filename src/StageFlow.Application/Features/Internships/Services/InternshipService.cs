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
    Task<Result> UpdateDraftAsync(Guid id, UpdateInternshipDraftCommand command, string studentId, CancellationToken cancellationToken);
    Task<Result> SubmitAsync(Guid id, string studentId, CancellationToken cancellationToken);
    Task<Result> ReopenAsync(Guid id, string studentId, CancellationToken cancellationToken);
    Task<Result> ApproveAsync(Guid id, string currentUserId, CancellationToken cancellationToken);
    Task<Result> RejectAsync(Guid id, string currentUserId, string rejectionReason, CancellationToken cancellationToken);
    Task<Result> StartInternshipAsync(Guid id, string currentUserId, CancellationToken cancellationToken);
    Task<Result> AssignSupervisorAsync(Guid id, string supervisorId, CancellationToken cancellationToken);
}

public class InternshipService : IInternshipService
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public InternshipService(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<InternshipDto>> GetAllAsync(string? studentId, string? supervisorId, InternshipStatus? status, Guid? trackId, CancellationToken cancellationToken)
    {
        var query = _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
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

        var internships = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        var names = await ResolveNamesAsync(internships, cancellationToken);

        return internships.Select(i => MapToDto(i, names)).ToList();
    }

    public async Task<Result<InternshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (internship == null)
            return Result<InternshipDto>.Failure("Dossier de stage introuvable.");

        var names = await ResolveNamesAsync(new[] { internship }, cancellationToken);
        return Result<InternshipDto>.Success(MapToDto(internship, names));
    }

    public async Task<Result<InternshipDto>> ProposeAsync(ProposeInternshipCommand command, string studentId, CancellationToken cancellationToken)
    {
        var company = await _context.Companies.FindAsync(new object[] { command.CompanyId }, cancellationToken);
        if (company == null)
            return Result<InternshipDto>.Failure("L'entreprise spécifiée n'existe pas.");

        var track = await _context.AcademicTracks.FindAsync(new object[] { command.AcademicTrackId }, cancellationToken);
        if (track == null)
            return Result<InternshipDto>.Failure("La filière spécifiée n'existe pas.");

        if (command.InternshipOfferId.HasValue)
        {
            var offer = await _context.InternshipOffers.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == command.InternshipOfferId.Value, cancellationToken);
            if (offer == null)
                return Result<InternshipDto>.Failure("L'offre de stage spécifiée n'existe pas.");
            if (!offer.IsActive || offer.ExpirationDate < DateTime.UtcNow)
                return Result<InternshipDto>.Failure("Cette offre de stage n'est plus ouverte aux candidatures.");
        }

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

            var names = await ResolveNamesAsync(new[] { internship }, cancellationToken);
            return Result<InternshipDto>.Success(MapToDto(internship, names, company.Name, track.Code));
        }
        catch (ArgumentException ex)
        {
            return Result<InternshipDto>.Failure(ex.Message);
        }
    }

    public async Task<Result> UpdateDraftAsync(Guid id, UpdateInternshipDraftCommand command, string studentId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        if (!await _context.Companies.AnyAsync(c => c.Id == command.CompanyId, cancellationToken))
            return Result.Failure("L'entreprise spécifiée n'existe pas.");

        var result = internship.UpdateDraft(
            command.Title,
            command.SubjectDescription,
            command.StartDate,
            command.EndDate,
            command.CompanyId,
            command.CompanySupervisorName,
            command.CompanySupervisorEmail,
            command.CompanySupervisorPhone,
            studentId);

        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> SubmitAsync(Guid id, string studentId, CancellationToken cancellationToken)
        => ApplyTransitionAsync(id, i => i.SubmitForApproval(studentId), cancellationToken);

    public Task<Result> ReopenAsync(Guid id, string studentId, CancellationToken cancellationToken)
        => ApplyTransitionAsync(id, i => i.ReopenAsDraft(studentId), cancellationToken);

    public async Task<Result> ApproveAsync(Guid id, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = internship.Approve(currentUserId);
        if (result.IsFailure) return result;

        // Génération de la convention à signer (idempotent : une seule convention par stage).
        if (!await _context.Conventions.AnyAsync(c => c.InternshipId == internship.Id, cancellationToken))
        {
            await _context.Conventions.AddAsync(Convention.CreateFor(internship.Id, DateTime.UtcNow), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> RejectAsync(Guid id, string currentUserId, string rejectionReason, CancellationToken cancellationToken)
        => ApplyTransitionAsync(id, i => i.Reject(currentUserId, rejectionReason), cancellationToken);

    public async Task<Result> StartInternshipAsync(Guid id, string currentUserId, CancellationToken cancellationToken)
    {
        // Un stage ne démarre qu'une fois la convention signée par l'élève, l'entreprise et l'école.
        var conventionSigned = await _context.Conventions
            .AnyAsync(c => c.InternshipId == id && c.SignedAt != null, cancellationToken);

        if (!conventionSigned)
            return Result.Failure("Le stage ne peut démarrer qu'après la signature de la convention par les trois parties.");

        return await ApplyTransitionAsync(id, i => i.StartInternship(currentUserId), cancellationToken);
    }

    public async Task<Result> AssignSupervisorAsync(Guid id, string supervisorId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        if (internship.Status is InternshipStatus.Refuse or InternshipStatus.Termine)
            return Result.Failure($"Impossible d'affecter un encadrant à un dossier au statut '{internship.Status}'.");

        internship.AssignAcademicSupervisor(supervisorId);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> ApplyTransitionAsync(Guid id, Func<Internship, Result> transition, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var result = transition(internship);
        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<Internship> internships, CancellationToken cancellationToken)
    {
        var ids = internships
            .SelectMany(i => new[] { i.StudentId, i.AcademicSupervisorId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();

        return _identityService.GetUserNamesAsync(ids, cancellationToken);
    }

    private static InternshipDto MapToDto(Internship i, IReadOnlyDictionary<string, string> names, string? companyName = null, string? trackCode = null)
    {
        return new InternshipDto
        {
            Id = i.Id,
            Title = i.Title,
            SubjectDescription = i.SubjectDescription,
            StartDate = i.Period.StartDate,
            EndDate = i.Period.EndDate,
            Status = i.Status,
            StatusName = i.Status.ToString(),
            RejectionReason = i.RejectionReason,
            StudentId = i.StudentId,
            StudentName = names.GetValueOrDefault(i.StudentId),
            AcademicSupervisorId = i.AcademicSupervisorId,
            AcademicSupervisorName = i.AcademicSupervisorId is null ? null : names.GetValueOrDefault(i.AcademicSupervisorId),
            CompanySupervisorName = i.CompanySupervisorName,
            CompanySupervisorEmail = i.CompanySupervisorEmail,
            CompanySupervisorPhone = i.CompanySupervisorPhone,
            CompanyId = i.CompanyId,
            CompanyName = companyName ?? i.Company?.Name ?? "Inconnue",
            AcademicTrackId = i.AcademicTrackId,
            AcademicTrackCode = trackCode ?? i.AcademicTrack?.Code ?? "N/A",
            InternshipOfferId = i.InternshipOfferId,
            Convention = i.Convention is null
                ? null
                : new ConventionStatusDto(
                    i.Convention.ConventionNumber,
                    i.Convention.IsSignedByStudent,
                    i.Convention.IsSignedByCompany,
                    i.Convention.IsSignedBySchool,
                    i.Convention.SignedAt)
        };
    }
}
