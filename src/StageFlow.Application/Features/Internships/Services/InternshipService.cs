using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Internships.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Internships.Services;

public interface IInternshipService
{
    Task<List<InternshipDto>> GetAllAsync(string? studentId, string? supervisorId, InternshipStatus? status, Guid? trackId, CancellationToken cancellationToken, string? academicYear = null);

    /// <summary>Années universitaires présentes dans le périmètre donné, de la plus récente à la plus ancienne.</summary>
    Task<List<string>> GetAcademicYearsAsync(Guid? trackId, CancellationToken cancellationToken);

    /// <summary>Annulation administrative (responsable de la filière ou administrateur), motif obligatoire.</summary>
    Task<Result> CancelAsync(Guid id, string currentUserId, string reason, CancellationToken cancellationToken);
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
    private readonly InternshipPolicy _policy;
    private readonly ITimeZoneService _timeZone;

    public InternshipService(
        IApplicationDbContext context,
        IIdentityService identityService,
        InternshipPolicy policy,
        ITimeZoneService timeZone)
    {
        _context = context;
        _identityService = identityService;
        _policy = policy;
        _timeZone = timeZone;
    }

    /// <summary>
    /// Contrôle de la période : début non passé (dans le fuseau de l'école), durée comprise entre les bornes
    /// réglementaires, et cohérente avec la durée annoncée par l'offre à laquelle le stage répond.
    /// </summary>
    private async Task<Result> ValidatePeriodAsync(DateTime start, DateTime end, Guid? offerId, CancellationToken cancellationToken)
    {
        if (end <= start)
            return Result.Failure("La date de fin doit être postérieure à la date de début.");

        if (_timeZone.ToLocal(start).Date < _timeZone.LocalToday)
            return Result.Failure("La date de début du stage ne peut pas être dans le passé.");

        var weeks = InternshipPolicy.DurationInWeeks(start, end);
        if (weeks < _policy.MinDurationWeeks)
            return Result.Failure($"La durée du stage ({weeks} semaine(s)) est inférieure au minimum réglementaire de {_policy.MinDurationWeeks} semaines.");

        if (weeks > _policy.MaxDurationWeeks)
            return Result.Failure($"La durée du stage ({weeks} semaines) dépasse le maximum autorisé de {_policy.MaxDurationWeeks} semaines.");

        if (offerId.HasValue)
        {
            var offerWeeks = await _context.InternshipOffers.AsNoTracking()
                .Where(o => o.Id == offerId.Value)
                .Select(o => (int?)o.DurationInWeeks)
                .FirstOrDefaultAsync(cancellationToken);

            if (offerWeeks.HasValue && Math.Abs(weeks - offerWeeks.Value) > _policy.OfferDurationToleranceWeeks)
                return Result.Failure($"La durée du stage ({weeks} semaines) ne correspond pas à celle de l'offre ({offerWeeks} semaines, tolérance ±{_policy.OfferDurationToleranceWeeks}).");
        }

        return Result.Success();
    }

    /// <summary>Un élève ne peut avoir qu'un seul dossier engagé (proposé, validé, en cours ou en soutenance).</summary>
    private async Task<Result> EnsureNoOtherEngagedInternshipAsync(string studentId, Guid? excludedId, CancellationToken cancellationToken)
    {
        var engaged = await _context.Internships.AsNoTracking()
            .Where(i => i.StudentId == studentId
                        && (!excludedId.HasValue || i.Id != excludedId.Value)
                        && Internship.EngagedStatuses.Contains(i.Status))
            .Select(i => new { i.Title, i.Status })
            .FirstOrDefaultAsync(cancellationToken);

        return engaged == null
            ? Result.Success()
            : Result.Failure($"Vous avez déjà un dossier engagé (« {engaged.Title} », statut {engaged.Status}). Un seul stage peut être suivi à la fois.");
    }

    public async Task<List<string>> GetAcademicYearsAsync(Guid? trackId, CancellationToken cancellationToken)
        => await _context.Internships.AsNoTracking()
            .Where(i => !trackId.HasValue || i.AcademicTrackId == trackId.Value)
            .Select(i => i.AcademicYear)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(cancellationToken);

    public Task<Result> CancelAsync(Guid id, string currentUserId, string reason, CancellationToken cancellationToken)
        => ApplyTransitionAsync(id, i => i.Cancel(currentUserId, reason), cancellationToken);

    public async Task<List<InternshipDto>> GetAllAsync(string? studentId, string? supervisorId, InternshipStatus? status, Guid? trackId, CancellationToken cancellationToken, string? academicYear = null)
    {
        var query = _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
            .Include(i => i.Documents)
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

        if (!string.IsNullOrWhiteSpace(academicYear))
            query = query.Where(i => i.AcademicYear == academicYear);

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
            .Include(i => i.Documents)
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
            if (offer.AcademicTrackId != command.AcademicTrackId)
                return Result<InternshipDto>.Failure("Cette offre est réservée à une autre filière.");
        }

        var engaged = await EnsureNoOtherEngagedInternshipAsync(studentId, null, cancellationToken);
        if (engaged.IsFailure)
            return Result<InternshipDto>.Failure(engaged.Error);

        var period = await ValidatePeriodAsync(command.StartDate, command.EndDate, command.InternshipOfferId, cancellationToken);
        if (period.IsFailure)
            return Result<InternshipDto>.Failure(period.Error);

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

        var period = await ValidatePeriodAsync(command.StartDate, command.EndDate, internship.InternshipOfferId, cancellationToken);
        if (period.IsFailure)
            return period;

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

    public async Task<Result> SubmitAsync(Guid id, string studentId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        var engaged = await EnsureNoOtherEngagedInternshipAsync(internship.StudentId, internship.Id, cancellationToken);
        if (engaged.IsFailure)
            return engaged;

        // Un brouillon ancien peut avoir une date de début désormais passée : elle est revérifiée à la soumission.
        var period = await ValidatePeriodAsync(internship.Period.StartDate, internship.Period.EndDate, internship.InternshipOfferId, cancellationToken);
        if (period.IsFailure)
            return period;

        var result = internship.SubmitForApproval(studentId);
        if (result.IsFailure) return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> ReopenAsync(Guid id, string studentId, CancellationToken cancellationToken)
        => ApplyTransitionAsync(id, i => i.ReopenAsDraft(studentId), cancellationToken);

    public async Task<Result> ApproveAsync(Guid id, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships.FindAsync(new object[] { id }, cancellationToken);
        if (internship == null)
            return Result.Failure("Dossier introuvable.");

        // Garde-fou : deux dossiers proposés du même élève ne peuvent pas être validés tous les deux.
        var alreadyValidated = await _context.Internships.AsNoTracking()
            .AnyAsync(i => i.StudentId == internship.StudentId
                           && i.Id != internship.Id
                           && (i.Status == InternshipStatus.Valide
                               || i.Status == InternshipStatus.EnCours
                               || i.Status == InternshipStatus.SoutenancePlanifiee), cancellationToken);
        if (alreadyValidated)
            return Result.Failure("Cet élève a déjà un stage validé ou en cours : un seul stage peut être suivi à la fois.");

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

        // L'encadrant doit appartenir à la filière du stage, ou être transversal (sans filière de rattachement).
        var supervisor = await _identityService.GetUserByIdAsync(supervisorId, cancellationToken);
        if (supervisor.IsFailure || !supervisor.Value!.IsActive)
            return Result.Failure("Encadrant introuvable ou inactif.");

        if (supervisor.Value.AcademicTrackId.HasValue && supervisor.Value.AcademicTrackId != internship.AcademicTrackId)
            return Result.Failure("Cet enseignant est rattaché à une autre filière : il ne peut pas encadrer ce stage.");

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
            CancellationReason = i.CancellationReason,
            AcademicYear = i.AcademicYear,
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
                    i.Convention.SignedAt,
                    i.Convention.CompanySignerName,
                    i.Convention.CompanySigningTokenExpiresAt > DateTime.UtcNow ? i.Convention.CompanySigningTokenExpiresAt : null),
            Documents = i.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new InternshipDocumentDto(d.Id, d.Kind.ToString(), d.FileName, d.SizeBytes, d.CreatedAt))
                .ToList(),
            TotalWeeks = i.TotalWeeks
        };
    }
}
