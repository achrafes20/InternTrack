using System.Text;
using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Features.Conventions.DTOs;
using InternTrack.Application.Features.Internships;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;

namespace InternTrack.Application.Features.Conventions.Services;

public interface IConventionAmendmentService
{
    Task<Result<List<ConventionAmendmentDto>>> GetForInternshipAsync(Guid internshipId, CancellationToken cancellationToken);

    /// <summary>Création par l'école (responsable de filière) : l'école est réputée signataire à la création.</summary>
    Task<Result<ConventionAmendmentDto>> CreateAsync(Guid internshipId, CreateConventionAmendmentCommand command, string currentUserId, CancellationToken cancellationToken);

    /// <summary>Signature de l'élève, ou enregistrement de la signature de l'entreprise (nom du signataire). Appliqué à la 3e signature.</summary>
    Task<Result<ConventionAmendmentDto>> SignAsync(Guid internshipId, Guid amendmentId, SignConventionAmendmentCommand command, CancellationToken cancellationToken);

    Task<Result> WithdrawAsync(Guid internshipId, Guid amendmentId, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// Avenants à la convention : toute modification des dates, du tuteur entreprise ou du sujet d'un stage
/// dont la convention est signée passe par un avenant signé par les trois parties.
/// </summary>
public class ConventionAmendmentService : IConventionAmendmentService
{
    private readonly IApplicationDbContext _context;
    private readonly InternshipPolicy _policy;
    private readonly ITimeZoneService _timeZone;

    public ConventionAmendmentService(IApplicationDbContext context, InternshipPolicy policy, ITimeZoneService timeZone)
    {
        _context = context;
        _policy = policy;
        _timeZone = timeZone;
    }

    public async Task<Result<List<ConventionAmendmentDto>>> GetForInternshipAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        var amendments = await _context.ConventionAmendments.AsNoTracking()
            .Where(a => a.Convention.InternshipId == internshipId)
            .OrderByDescending(a => a.Number)
            .ToListAsync(cancellationToken);

        return Result<List<ConventionAmendmentDto>>.Success(amendments.Select(ToDto).ToList());
    }

    public async Task<Result<ConventionAmendmentDto>> CreateAsync(Guid internshipId, CreateConventionAmendmentCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships
            .Include(i => i.Convention)
                .ThenInclude(c => c!.Amendments)
            .FirstOrDefaultAsync(i => i.Id == internshipId, cancellationToken);

        if (internship == null)
            return Result<ConventionAmendmentDto>.Failure("Stage introuvable.");

        var convention = internship.Convention;
        if (convention == null || !convention.IsFullySigned)
            return Result<ConventionAmendmentDto>.Failure("Un avenant ne concerne qu'une convention signée par les trois parties. Avant signature, le dossier se corrige directement.");
        if (convention.IsTerminated)
            return Result<ConventionAmendmentDto>.Failure("La convention est résiliée : aucun avenant n'est possible.");
        if (!Internship.AmendableStatuses.Contains(internship.Status))
            return Result<ConventionAmendmentDto>.Failure($"Un avenant ne peut concerner qu'un stage validé ou en cours (Statut : {internship.Status}). Annulez d'abord la soutenance planifiée si les dates doivent changer.");
        if (convention.Amendments.Any(a => a.IsPending))
            return Result<ConventionAmendmentDto>.Failure("Un avenant est déjà en attente de signature : faites-le signer ou retirez-le avant d'en créer un autre.");
        if (string.IsNullOrWhiteSpace(command.Reason))
            return Result<ConventionAmendmentDto>.Failure("Le motif de l'avenant est obligatoire.");

        // Seules les valeurs réellement modifiées sont retenues.
        var newStart = command.NewStartDate is { } s && s != internship.Period.StartDate ? s : (DateTime?)null;
        var newEnd = command.NewEndDate is { } e && e != internship.Period.EndDate ? e : (DateTime?)null;
        var newTitle = Changed(command.NewTitle, internship.Title);
        var newSubject = Changed(command.NewSubjectDescription, internship.SubjectDescription);
        var newTutorName = Changed(command.NewCompanySupervisorName, internship.CompanySupervisorName);
        var newTutorEmail = Changed(command.NewCompanySupervisorEmail, internship.CompanySupervisorEmail);
        var newTutorPhone = command.NewCompanySupervisorPhone is null
            ? null
            : (command.NewCompanySupervisorPhone.Trim() != (internship.CompanySupervisorPhone ?? string.Empty) ? command.NewCompanySupervisorPhone.Trim() : null);

        if (newStart == null && newEnd == null && newTitle == null && newSubject == null
            && newTutorName == null && newTutorEmail == null && newTutorPhone == null)
            return Result<ConventionAmendmentDto>.Failure("L'avenant ne modifie rien : renseignez au moins une nouvelle valeur.");

        var periodCheck = ValidatePeriod(internship, newStart, newEnd);
        if (periodCheck.IsFailure)
            return Result<ConventionAmendmentDto>.Failure(periodCheck.Error);

        var summary = BuildSummary(internship, newStart, newEnd, newTitle, newSubject, newTutorName, newTutorEmail, newTutorPhone);
        var number = convention.Amendments.Count == 0 ? 1 : convention.Amendments.Max(a => a.Number) + 1;

        var amendment = new ConventionAmendment(
            convention.Id, number, command.Reason, currentUserId,
            newStart, newEnd, newTitle, newSubject, newTutorName, newTutorEmail, newTutorPhone,
            summary);

        // L'école, à l'initiative de l'avenant, le signe à sa création.
        amendment.Sign(ConventionParty.School);

        await _context.ConventionAmendments.AddAsync(amendment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<ConventionAmendmentDto>.Success(ToDto(amendment));
    }

    public async Task<Result<ConventionAmendmentDto>> SignAsync(Guid internshipId, Guid amendmentId, SignConventionAmendmentCommand command, CancellationToken cancellationToken)
    {
        var amendment = await _context.ConventionAmendments
            .Include(a => a.Convention)
                .ThenInclude(c => c.Internship)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Convention.InternshipId == internshipId, cancellationToken);

        if (amendment == null)
            return Result<ConventionAmendmentDto>.Failure("Avenant introuvable.");

        var signed = amendment.Sign(command.Party, command.SignerName);
        if (signed.IsFailure)
            return Result<ConventionAmendmentDto>.Failure(signed.Error);

        if (amendment.IsFullySigned)
        {
            var internship = amendment.Convention.Internship;
            var periodCheck = ValidatePeriod(internship, amendment.NewStartDate, amendment.NewEndDate);
            if (periodCheck.IsFailure)
                return Result<ConventionAmendmentDto>.Failure($"L'avenant ne peut plus être appliqué : {periodCheck.Error} Retirez-le et créez-en un nouveau.");

            var applied = internship.ApplyAmendment(
                amendment.NewStartDate,
                amendment.NewEndDate,
                amendment.NewTitle,
                amendment.NewSubjectDescription,
                amendment.NewCompanySupervisorName,
                amendment.NewCompanySupervisorEmail,
                amendment.NewCompanySupervisorPhone);
            if (applied.IsFailure)
                return Result<ConventionAmendmentDto>.Failure(applied.Error);

            amendment.MarkApplied(DateTime.UtcNow);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<ConventionAmendmentDto>.Success(ToDto(amendment));
    }

    public async Task<Result> WithdrawAsync(Guid internshipId, Guid amendmentId, string reason, CancellationToken cancellationToken)
    {
        var amendment = await _context.ConventionAmendments
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Convention.InternshipId == internshipId, cancellationToken);

        if (amendment == null)
            return Result.Failure("Avenant introuvable.");

        var result = amendment.Withdraw(reason, DateTime.UtcNow);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Mêmes bornes que lors de la proposition : durée réglementaire ; une nouvelle date de début ne peut être
    /// ni passée ni appliquée à un stage déjà démarré ; une nouvelle date de fin ne peut pas être passée.
    /// </summary>
    private Result ValidatePeriod(Internship internship, DateTime? newStart, DateTime? newEnd)
    {
        if (newStart == null && newEnd == null)
            return Result.Success();

        var start = newStart ?? internship.Period.StartDate;
        var end = newEnd ?? internship.Period.EndDate;
        var today = _timeZone.LocalToday;

        if (end <= start)
            return Result.Failure("La date de fin doit être postérieure à la date de début.");

        if (newStart.HasValue)
        {
            if (internship.Status != InternshipStatus.Valide)
                return Result.Failure("La date de début ne peut plus être modifiée : le stage a déjà démarré.");
            if (_timeZone.ToLocal(newStart.Value).Date < today)
                return Result.Failure("La nouvelle date de début ne peut pas être dans le passé.");
        }

        if (newEnd.HasValue && _timeZone.ToLocal(newEnd.Value).Date < today)
            return Result.Failure("La nouvelle date de fin ne peut pas être dans le passé.");

        var weeks = InternshipPolicy.DurationInWeeks(start, end);
        if (weeks < _policy.MinDurationWeeks)
            return Result.Failure($"La durée du stage ({weeks} semaine(s)) serait inférieure au minimum réglementaire de {_policy.MinDurationWeeks} semaines.");
        if (weeks > _policy.MaxDurationWeeks)
            return Result.Failure($"La durée du stage ({weeks} semaines) dépasserait le maximum autorisé de {_policy.MaxDurationWeeks} semaines.");

        return Result.Success();
    }

    private static string? Changed(string? proposed, string current)
        => string.IsNullOrWhiteSpace(proposed) || proposed.Trim() == current ? null : proposed.Trim();

    private string BuildSummary(
        Internship internship, DateTime? newStart, DateTime? newEnd, string? newTitle, string? newSubject,
        string? newTutorName, string? newTutorEmail, string? newTutorPhone)
    {
        var sb = new StringBuilder();
        string D(DateTime d) => _timeZone.ToLocal(d).ToString("dd/MM/yyyy");

        if (newStart.HasValue) sb.AppendLine($"Date de début : {D(internship.Period.StartDate)} → {D(newStart.Value)}");
        if (newEnd.HasValue) sb.AppendLine($"Date de fin : {D(internship.Period.EndDate)} → {D(newEnd.Value)}");
        if (newTitle != null) sb.AppendLine($"Titre : « {internship.Title} » → « {newTitle} »");
        if (newSubject != null) sb.AppendLine("Description du sujet : reformulée");
        if (newTutorName != null) sb.AppendLine($"Tuteur entreprise : {internship.CompanySupervisorName} → {newTutorName}");
        if (newTutorEmail != null) sb.AppendLine($"E-mail du tuteur : {internship.CompanySupervisorEmail} → {newTutorEmail}");
        if (newTutorPhone != null) sb.AppendLine($"Téléphone du tuteur : {internship.CompanySupervisorPhone ?? "—"} → {(newTutorPhone.Length == 0 ? "—" : newTutorPhone)}");

        return sb.ToString().TrimEnd();
    }

    private static ConventionAmendmentDto ToDto(ConventionAmendment a) => new(
        a.Id, a.Number, a.Reason, a.ChangesSummary,
        a.NewStartDate, a.NewEndDate, a.NewTitle, a.NewSubjectDescription,
        a.NewCompanySupervisorName, a.NewCompanySupervisorEmail, a.NewCompanySupervisorPhone,
        a.IsSignedByStudent, a.IsSignedByCompany, a.IsSignedBySchool, a.CompanySignerName,
        a.CreatedAt, a.AppliedAt, a.WithdrawnAt, a.WithdrawalReason,
        a.AppliedAt != null ? "Applique" : a.WithdrawnAt != null ? "Retire" : "EnAttente");
}
