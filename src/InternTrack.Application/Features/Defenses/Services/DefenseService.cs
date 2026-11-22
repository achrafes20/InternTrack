using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Features.Defenses.DTOs;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Domain.ValueObjects;
using InternTrack.Application.Features.Defenses;

namespace InternTrack.Application.Features.Defenses.Services;

public interface IDefenseService
{
    Task<List<DefenseDto>> GetAllDefensesAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken);
    Task<Result<DefenseDto>> GetDefenseByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<DefenseDto>> ScheduleDefenseAsync(ScheduleDefenseCommand command, string currentUserId, CancellationToken cancellationToken);
    Task<ConflictCheckResultDto> CheckConflictAsync(CheckConflictRequest request, CancellationToken cancellationToken);
    Task<Result> RecordGradeAsync(Guid defenseId, RecordGradeCommand command, string currentUserId, CancellationToken cancellationToken);

    /// <summary>Report d'une soutenance non notée (absence, maladie…) : libère le créneau, motif obligatoire.</summary>
    Task<Result> CancelDefenseAsync(Guid defenseId, string reason, string currentUserId, CancellationToken cancellationToken);
    Task<Result> CorrectGradeAsync(Guid defenseId, CorrectGradeCommand command, string currentUserId, CancellationToken cancellationToken);
    Task<Result> SubmitEvaluationAsync(Guid defenseId, SubmitEvaluationCommand command, string teacherId, CancellationToken cancellationToken);
    Task<bool> IsJuryPresidentAsync(Guid defenseId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Enseignants actifs. Avec <paramref name="trackId"/> : ceux de la filière et les enseignants transversaux
    /// (sans filière de rattachement), seuls habilités à encadrer un stage de cette filière.
    /// </summary>
    Task<List<TeacherDto>> GetTeachersAsync(Guid? trackId, CancellationToken cancellationToken);
}

public class DefenseService : IDefenseService
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 240;

    private static readonly string[] TeacherRoles = { UserRole.Encadrant, UserRole.ResponsableFiliere };

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly DefensePolicy _policy;
    private readonly TimeProvider _clock;

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public DefenseService(IApplicationDbContext context, IIdentityService identityService, DefensePolicy? policy = null, TimeProvider? clock = null)
    {
        _context = context;
        _identityService = identityService;
        _policy = policy ?? new DefensePolicy();
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<List<TeacherDto>> GetTeachersAsync(Guid? trackId, CancellationToken cancellationToken)
    {
        var users = await _identityService.GetUsersInRolesAsync(TeacherRoles, cancellationToken);
        return users
            .Where(u => u.IsActive)
            .Where(u => !trackId.HasValue || u.AcademicTrackId == null || u.AcademicTrackId == trackId.Value)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new TeacherDto(u.Id, u.FullName, u.Email, u.Role, u.AcademicTrackId))
            .ToList();
    }

    public async Task<bool> IsJuryPresidentAsync(Guid defenseId, string userId, CancellationToken cancellationToken)
        => await _context.DefenseJuryMembers.AsNoTracking()
            .AnyAsync(m => m.DefenseId == defenseId && m.TeacherId == userId && m.Role == DefenseRole.President, cancellationToken);

    public async Task<Result> SubmitEvaluationAsync(Guid defenseId, SubmitEvaluationCommand command, string teacherId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.JuryMembers)
            .Include(d => d.Evaluations)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);

        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        if (!await HasThesisAsync(defense.InternshipId, cancellationToken))
            return Result.Failure("Aucun mémoire n'a été déposé : le jury ne peut pas encore évaluer.");

        var result = defense.SubmitEvaluation(teacherId, command.Grade, command.Comment, UtcNow);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CorrectGradeAsync(Guid defenseId, CorrectGradeCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);
        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        // L'ancienne et la nouvelle note sont tracées par le journal d'audit (immuable).
        var result = defense.CorrectGrade(command.Grade, command.Comments, command.Reason, UtcNow, currentUserId);
        if (result.IsFailure)
            return result;

        // Une délibération peut changer le résultat : validé ↔ ajourné / non validé.
        var outcome = defense.Internship.ReviseDefenseOutcome(
            _policy.IsPassing(command.Grade),
            _policy.CanRetakeAfter(defense.Attempt),
            currentUserId);
        if (outcome.IsFailure)
            return outcome;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<List<DefenseDto>> GetAllDefensesAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken)
    {
        var query = _context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .Include(d => d.Evaluations)
            .AsNoTracking()
            .AsQueryable();

        // Périmètre de visibilité selon le rôle (anti-IDOR).
        if (role == UserRole.Etudiant)
        {
            query = query.Where(d => d.Internship.StudentId == userId);
        }
        else if (role == UserRole.Encadrant)
        {
            query = query.Where(d =>
                d.Internship.AcademicSupervisorId == userId ||
                d.JuryMembers.Any(m => m.TeacherId == userId));
        }
        else if (role == UserRole.ResponsableFiliere)
        {
            query = academicTrackId.HasValue
                ? query.Where(d => d.Internship.AcademicTrackId == academicTrackId.Value)
                : query.Where(_ => false);
        }
        else if (role != UserRole.Admin)
        {
            query = query.Where(_ => false);
        }

        var defenses = await query
            .OrderBy(d => d.ScheduledAt)
            .ToListAsync(cancellationToken);

        var names = await _identityService.GetUserNamesAsync(defenses.Select(d => d.Internship.StudentId).Distinct(), cancellationToken);

        // Les évaluations individuelles du jury restent confidentielles pour l'élève : il ne voit que la note finale.
        var includeEvaluations = role != UserRole.Etudiant;
        return defenses.Select(d => MapToDto(d, d.Internship, names, includeEvaluations: includeEvaluations)).ToList();
    }

    public async Task<Result<DefenseDto>> GetDefenseByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .Include(d => d.Evaluations)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (defense == null)
            return Result<DefenseDto>.Failure("Soutenance introuvable.");

        var names = await _identityService.GetUserNamesAsync(new[] { defense.Internship.StudentId }, cancellationToken);
        return Result<DefenseDto>.Success(MapToDto(defense, defense.Internship, names));
    }

    public async Task<ConflictCheckResultDto> CheckConflictAsync(CheckConflictRequest request, CancellationToken cancellationToken)
    {
        var duration = Math.Clamp(request.DurationMinutes, MinDurationMinutes, MaxDurationMinutes);

        Guid? excludedDefenseId = null;
        if (request.InternshipId.HasValue)
        {
            excludedDefenseId = await _context.Defenses
                .Where(d => d.InternshipId == request.InternshipId.Value)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var messages = await FindConflictsAsync(
            request.ScheduledAt,
            duration,
            request.Classroom,
            request.JuryTeacherIds ?? new List<string>(),
            excludedDefenseId,
            stopAtFirst: false,
            cancellationToken);

        return new ConflictCheckResultDto(messages.Count > 0, messages);
    }

    /// <summary>
    /// La vérification des conflits et l'écriture sont exécutées sous un verrou exclusif : deux responsables
    /// qui planifient au même instant sont traités l'un après l'autre, le second voyant la soutenance du premier.
    /// La contrainte d'exclusion PostgreSQL sur (salle, créneau) reste un filet de sécurité au niveau base.
    /// </summary>
    public Task<Result<DefenseDto>> ScheduleDefenseAsync(ScheduleDefenseCommand command, string currentUserId, CancellationToken cancellationToken)
        => _context.RunExclusiveAsync(
            LockNames.DefenseScheduling,
            () => ScheduleDefenseCoreAsync(command, currentUserId, cancellationToken),
            cancellationToken);

    private async Task<Result<DefenseDto>> ScheduleDefenseCoreAsync(ScheduleDefenseCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships
            .Include(i => i.Defense)
                .ThenInclude(d => d!.JuryMembers)
            .Include(i => i.Defense)
                .ThenInclude(d => d!.Evaluations)
            .FirstOrDefaultAsync(i => i.Id == command.InternshipId, cancellationToken);

        if (internship == null)
            return Result<DefenseDto>.Failure("Stage introuvable.");

        var existing = internship.Defense;

        if (internship.Status is not (InternshipStatus.EnCours or InternshipStatus.SoutenancePlanifiee or InternshipStatus.Ajourne))
            return Result<DefenseDto>.Failure($"La soutenance ne peut être planifiée que pour un stage en cours ou ajourné (Statut : {internship.Status}).");

        // Nature de l'opération : première planification, replanification, ou session de rattrapage.
        var opensNewRetake = internship.Status == InternshipStatus.Ajourne && existing is { FinalGrade: not null };
        var isRetake = opensNewRetake || existing is { IsRetake: true };

        if (existing?.FinalGrade != null && !opensNewRetake)
            return Result<DefenseDto>.Failure("Cette soutenance a déjà été notée et ne peut plus être replanifiée.");

        if (opensNewRetake && !_policy.CanRetakeAfter(existing!.Attempt))
            return Result<DefenseDto>.Failure($"Le nombre maximal de sessions ({_policy.MaxAttempts}) est atteint : aucun rattrapage n'est possible.");

        if (string.IsNullOrWhiteSpace(command.Classroom))
            return Result<DefenseDto>.Failure("La salle est obligatoire.");

        if (command.DurationMinutes < MinDurationMinutes || command.DurationMinutes > MaxDurationMinutes)
            return Result<DefenseDto>.Failure($"La durée doit être comprise entre {MinDurationMinutes} et {MaxDurationMinutes} minutes.");

        var dateCheck = ValidateDefenseDate(internship, existing, command.ScheduledAt, isRetake);
        if (dateCheck.IsFailure)
            return Result<DefenseDto>.Failure(dateCheck.Error);

        var reportsCheck = await CheckValidatedReportsAsync(internship, UtcNow, cancellationToken);
        if (reportsCheck.IsFailure)
            return Result<DefenseDto>.Failure(reportsCheck.Error);

        var juryValidation = await ValidateJuryAsync(command.JuryMembers, cancellationToken);
        if (juryValidation.IsFailure)
            return Result<DefenseDto>.Failure(juryValidation.Error);

        var jury = juryValidation.Value!;
        var classroom = command.Classroom.Trim();

        var conflicts = await FindConflictsAsync(
            command.ScheduledAt,
            command.DurationMinutes,
            classroom,
            jury.Where(m => m.Role != DefenseRole.TuteurEntreprise).Select(m => m.TeacherId),
            existing?.Id,
            stopAtFirst: true,
            cancellationToken);

        if (conflicts.Count > 0)
            return Result<DefenseDto>.Failure(conflicts[0]);

        var defense = existing;
        if (defense == null)
        {
            defense = new Defense(command.InternshipId, command.ScheduledAt, classroom, command.DurationMinutes);
            await _context.Defenses.AddAsync(defense, cancellationToken);
        }
        else
        {
            if (opensNewRetake)
            {
                // Les évaluations de la session précédente sont écartées : le jury évalue à nouveau.
                _context.DefenseEvaluations.RemoveRange(defense.Evaluations.ToList());
                var retake = defense.StartRetake(command.ScheduledAt, classroom, command.DurationMinutes, _policy.MaxAttempts);
                if (retake.IsFailure)
                    return Result<DefenseDto>.Failure(retake.Error);
            }
            else
            {
                var reschedule = defense.Reschedule(command.ScheduledAt, classroom, command.DurationMinutes);
                if (reschedule.IsFailure)
                    return Result<DefenseDto>.Failure(reschedule.Error);
            }

            _context.DefenseJuryMembers.RemoveRange(defense.JuryMembers.ToList());
        }

        var newMembers = jury
            .Select(m => new DefenseJuryMember(defense.Id, m.TeacherId, m.TeacherFullName, m.Role))
            .ToList();
        await _context.DefenseJuryMembers.AddRangeAsync(newMembers, cancellationToken);

        var transition = internship.Status switch
        {
            InternshipStatus.EnCours => internship.MarkDefenseScheduled(currentUserId),
            InternshipStatus.Ajourne => internship.MarkRetakeScheduled(currentUserId),
            _ => Result.Success()
        };
        if (transition.IsFailure)
            return Result<DefenseDto>.Failure(transition.Error);

        await _context.SaveChangesAsync(cancellationToken);

        var names = await _identityService.GetUserNamesAsync(new[] { internship.StudentId }, cancellationToken);
        return Result<DefenseDto>.Success(MapToDto(defense, internship, names, newMembers));
    }

    /// <summary>
    /// Fenêtre réglementaire : date future, au plus tôt N jours avant la fin du stage, au plus tard N jours
    /// après (délai plus long pour un rattrapage, qui doit suivre la session précédente).
    /// </summary>
    private Result ValidateDefenseDate(Internship internship, Defense? existing, DateTime scheduledAt, bool isRetake)
    {
        if (scheduledAt <= UtcNow)
            return Result.Failure("La soutenance doit être planifiée à une date future.");

        var end = internship.Period.EndDate;
        var earliest = end.AddDays(-_policy.EarliestDaysBeforeEnd);
        var latestDays = isRetake ? _policy.RetakeLatestDaysAfterEnd : _policy.LatestDaysAfterEnd;
        var latest = end.AddDays(latestDays);

        if (!isRetake && scheduledAt < earliest)
            return Result.Failure($"La soutenance ne peut pas avoir lieu plus de {_policy.EarliestDaysBeforeEnd} jours avant la fin du stage (au plus tôt le {earliest:dd/MM/yyyy}).");

        if (scheduledAt > latest)
            return Result.Failure($"La soutenance doit avoir lieu au plus tard {latestDays} jours après la fin du stage (le {latest:dd/MM/yyyy}).");

        var previousSessionAt = existing?.FinalGrade != null ? existing.GradedAt : existing?.PreviousAttemptGradedAt;
        if (isRetake && previousSessionAt.HasValue && scheduledAt <= previousSessionAt.Value)
            return Result.Failure("Le rattrapage doit avoir lieu après la session précédente.");

        return Result.Success();
    }

    /// <summary>Nombre de semaines du stage commencées à la date donnée (borné à la durée du stage).</summary>
    private static int ElapsedWeeks(Internship internship, DateTime at)
        => Enumerable.Range(1, internship.TotalWeeks).Count(w => internship.WeekStart(w) <= at);

    private async Task<Result> CheckValidatedReportsAsync(Internship internship, DateTime at, CancellationToken cancellationToken)
    {
        var required = _policy.RequiredValidatedReports(ElapsedWeeks(internship, at));
        if (required == 0)
            return Result.Success();

        var validated = await _context.ProgressReports.AsNoTracking()
            .CountAsync(r => r.InternshipId == internship.Id && r.Status == ReportStatus.Valide, cancellationToken);

        return validated >= required
            ? Result.Success()
            : Result.Failure($"Suivi insuffisant : {validated} rapport(s) hebdomadaire(s) validé(s) sur {required} exigé(s). L'encadrant doit valider les rapports avant la soutenance.");
    }

    private Task<bool> HasThesisAsync(Guid internshipId, CancellationToken cancellationToken)
        => _context.InternshipDocuments.AsNoTracking()
            .AnyAsync(d => d.InternshipId == internshipId && d.Kind == InternshipDocumentKind.FinalThesis, cancellationToken);

    /// <summary>
    /// Report d'une soutenance non notée (absence, maladie, mémoire non prêt) : le créneau est libéré et le
    /// dossier revient « en cours » (ou « ajourné » s'il s'agissait d'un rattrapage).
    /// </summary>
    public async Task<Result> CancelDefenseAsync(Guid defenseId, string reason, string currentUserId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);

        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        var cancel = defense.Cancel(reason, UtcNow);
        if (cancel.IsFailure)
            return cancel;

        var revert = defense.Internship.RevertDefenseScheduling(defense.IsRetake, currentUserId);
        if (revert.IsFailure)
            return revert;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Arrêt de la note finale. Elle n'est pas saisie librement : elle est calculée à partir des évaluations
    /// du jury (et du tuteur entreprise, pondérée). Le mémoire et le suivi hebdomadaire sont exigés.
    /// Le résultat (terminé, ajourné, non validé) découle du seuil de validation du règlement.
    /// </summary>
    public async Task<Result> RecordGradeAsync(Guid defenseId, RecordGradeCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
            .Include(d => d.Evaluations)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);

        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        if (defense.IsCancelled)
            return Result.Failure("Cette soutenance a été annulée : replanifiez-la avant d'arrêter la note.");

        if (!await HasThesisAsync(defense.InternshipId, cancellationToken))
            return Result.Failure("Aucun mémoire n'a été déposé : la note ne peut pas être arrêtée.");

        var reportsCheck = await CheckValidatedReportsAsync(defense.Internship, defense.ScheduledAt, cancellationToken);
        if (reportsCheck.IsFailure)
            return reportsCheck;

        var computed = defense.ComputeFinalGrade(_policy.TutorWeight, _policy.RequireTutorEvaluation);
        if (computed.IsFailure)
            return Result.Failure(computed.Error);

        var grading = defense.RecordGrade(computed.Value, command.Comments, UtcNow, currentUserId);
        if (grading.IsFailure)
            return grading;

        var outcome = defense.Internship.RecordDefenseOutcome(
            _policy.IsPassing(computed.Value),
            _policy.CanRetakeAfter(defense.Attempt),
            currentUserId);
        if (outcome.IsFailure)
            return outcome;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private sealed record ValidatedJuryMember(string TeacherId, string TeacherFullName, DefenseRole Role);

    private async Task<Result<List<ValidatedJuryMember>>> ValidateJuryAsync(List<JuryMemberInput>? members, CancellationToken cancellationToken)
    {
        if (members == null || members.Count < 2 || members.Count > 4)
            return Result<List<ValidatedJuryMember>>.Failure("Un jury doit comporter entre 2 et 4 membres.");

        if (members.Any(m => !Enum.IsDefined(m.Role)))
            return Result<List<ValidatedJuryMember>>.Failure("Rôle de jury invalide.");

        if (members.Count(m => m.Role == DefenseRole.President) != 1)
            return Result<List<ValidatedJuryMember>>.Failure("Le jury doit comporter exactement un président.");

        var teachers = (await _identityService.GetUsersInRolesAsync(TeacherRoles, cancellationToken))
            .Where(u => u.IsActive)
            .ToDictionary(u => u.Id);

        var validated = new List<ValidatedJuryMember>();
        foreach (var member in members)
        {
            if (member.Role == DefenseRole.TuteurEntreprise)
            {
                if (string.IsNullOrWhiteSpace(member.TeacherFullName))
                    return Result<List<ValidatedJuryMember>>.Failure("Le nom du tuteur entreprise est obligatoire.");

                // Membre externe sans compte : identifiant technique propre à ce jury.
                validated.Add(new ValidatedJuryMember($"external:{Guid.NewGuid():N}", member.TeacherFullName.Trim(), member.Role));
                continue;
            }

            if (string.IsNullOrWhiteSpace(member.TeacherId) || !teachers.TryGetValue(member.TeacherId, out var teacher))
                return Result<List<ValidatedJuryMember>>.Failure("Chaque membre interne du jury doit être un enseignant enregistré (Encadrant ou Responsable de filière).");

            validated.Add(new ValidatedJuryMember(teacher.Id, teacher.FullName, member.Role));
        }

        if (validated.Select(m => m.TeacherId).Distinct().Count() != validated.Count)
            return Result<List<ValidatedJuryMember>>.Failure("Un même enseignant ne peut pas occuper deux places dans le jury.");

        return Result<List<ValidatedJuryMember>>.Success(validated);
    }

    /// <summary>
    /// Recherche les conflits de salle et d'enseignants. Le chevauchement est calculé en SQL
    /// (début existant &lt; fin demandée ET fin existante &gt; début demandé) : seules les soutenances
    /// réellement en conflit sont chargées.
    /// </summary>
    private async Task<List<string>> FindConflictsAsync(
        DateTime start,
        int durationMinutes,
        string classroom,
        IEnumerable<string> teacherIds,
        Guid? excludedDefenseId,
        bool stopAtFirst,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        var proposedRange = new DateRange(start, start.AddMinutes(durationMinutes));
        var proposedStart = proposedRange.StartDate;
        var proposedEnd = proposedRange.EndDate;

        var overlapping = await _context.Defenses
            .AsNoTracking()
            .Include(d => d.JuryMembers)
            .Where(d => d.CancelledAt == null)
            .Where(d => d.ScheduledAt < proposedEnd && d.EndsAt > proposedStart)
            .Where(d => !excludedDefenseId.HasValue || d.Id != excludedDefenseId.Value)
            .ToListAsync(cancellationToken);
        var normalizedRoom = (classroom ?? string.Empty).Trim();

        var roomConflict = string.IsNullOrEmpty(normalizedRoom)
            ? null
            : overlapping.FirstOrDefault(d => string.Equals(d.Classroom.Trim(), normalizedRoom, StringComparison.OrdinalIgnoreCase));

        if (roomConflict != null)
        {
            messages.Add($"La salle '{roomConflict.Classroom}' est déjà réservée sur ce créneau horaire.");
            if (stopAtFirst) return messages;
        }

        var ids = teacherIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet();
        if (ids.Count == 0) return messages;

        foreach (var conflict in overlapping
                     .SelectMany(d => d.JuryMembers)
                     .Where(m => ids.Contains(m.TeacherId))
                     .GroupBy(m => m.TeacherId)
                     .Select(g => g.First()))
        {
            messages.Add($"L'enseignant '{conflict.TeacherFullName}' fait déjà partie d'un jury sur ce créneau horaire.");
            if (stopAtFirst) return messages;
        }

        return messages;
    }

    private DefenseDto MapToDto(
        Defense d,
        Internship internship,
        IReadOnlyDictionary<string, string> names,
        IEnumerable<DefenseJuryMember>? juryMembers = null,
        bool includeEvaluations = true)
    {
        var policy = _policy;
        var evaluations = includeEvaluations
            ? d.Evaluations
                .OrderBy(e => e.TeacherFullName)
                .Select(e => new DefenseEvaluationDto(e.TeacherId, e.TeacherFullName, e.Grade, e.Comment, e.LastModifiedAt ?? e.CreatedAt))
                .ToList()
            : new List<DefenseEvaluationDto>();

        return new DefenseDto(
            d.Id,
            d.InternshipId,
            internship.StudentId,
            names.GetValueOrDefault(internship.StudentId) ?? "Élève ingénieur",
            internship.Title,
            d.ScheduledAt,
            d.DurationMinutes,
            d.Classroom,
            d.FinalGrade,
            d.Comments,
            (juryMembers ?? d.JuryMembers)
                .OrderBy(m => m.Role)
                .Select(m => new JuryMemberDto(m.Id, m.TeacherId, m.TeacherFullName, m.Role, m.Role.ToString()))
                .ToList(),
            evaluations,
            includeEvaluations ? d.AverageEvaluation : null,
            d.GradedAt,
            d.GradeCorrectionReason,
            d.GradeCorrectedAt)
        {
            Attempt = d.Attempt,
            PreviousAttemptGrade = d.PreviousAttemptGrade,
            CancelledAt = d.CancelledAt,
            CancellationReason = d.CancellationReason,
            InternshipStatus = internship.Status.ToString(),
            Passed = d.FinalGrade.HasValue ? policy.IsPassing(d.FinalGrade.Value) : null,
            PassingGrade = policy.PassingGrade,
            TutorWeight = policy.TutorWeight,
            ProvisionalGrade = includeEvaluations && !d.FinalGrade.HasValue && d.JuryMembers.Count > 0
                ? d.ComputeFinalGrade(policy.TutorWeight, policy.RequireTutorEvaluation) is { IsSuccess: true } c ? c.Value : null
                : null,
            MissingEvaluations = includeEvaluations && !d.FinalGrade.HasValue
                ? (juryMembers ?? d.JuryMembers)
                    .Where(m => d.Evaluations.All(e => e.TeacherId != m.TeacherId))
                    .Select(m => m.TeacherFullName)
                    .ToList()
                : new List<string>()
        };
    }
}
