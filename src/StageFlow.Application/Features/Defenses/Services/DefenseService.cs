using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Domain.ValueObjects;

namespace StageFlow.Application.Features.Defenses.Services;

public interface IDefenseService
{
    Task<List<DefenseDto>> GetAllDefensesAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken);
    Task<Result<DefenseDto>> GetDefenseByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<DefenseDto>> ScheduleDefenseAsync(ScheduleDefenseCommand command, string currentUserId, CancellationToken cancellationToken);
    Task<ConflictCheckResultDto> CheckConflictAsync(CheckConflictRequest request, CancellationToken cancellationToken);
    Task<Result> RecordGradeAsync(Guid defenseId, RecordGradeCommand command, string currentUserId, CancellationToken cancellationToken);
    Task<List<TeacherDto>> GetTeachersAsync(CancellationToken cancellationToken);
}

public class DefenseService : IDefenseService
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 240;

    private static readonly string[] TeacherRoles = { UserRole.Encadrant, UserRole.ResponsableFiliere };

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public DefenseService(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<TeacherDto>> GetTeachersAsync(CancellationToken cancellationToken)
    {
        var users = await _identityService.GetUsersInRolesAsync(TeacherRoles, cancellationToken);
        return users
            .Where(u => u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new TeacherDto(u.Id, u.FullName, u.Email, u.Role, u.AcademicTrackId))
            .ToList();
    }

    public async Task<List<DefenseDto>> GetAllDefensesAsync(string userId, string role, Guid? academicTrackId, CancellationToken cancellationToken)
    {
        var query = _context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
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
        return defenses.Select(d => MapToDto(d, d.Internship, names)).ToList();
    }

    public async Task<Result<DefenseDto>> GetDefenseByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .Include(d => d.JuryMembers)
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
            .FirstOrDefaultAsync(i => i.Id == command.InternshipId, cancellationToken);

        if (internship == null)
            return Result<DefenseDto>.Failure("Stage introuvable.");

        if (internship.Status != InternshipStatus.EnCours && internship.Status != InternshipStatus.SoutenancePlanifiee)
            return Result<DefenseDto>.Failure($"La soutenance ne peut être planifiée que pour un stage en cours (Statut : {internship.Status}).");

        if (internship.Defense?.FinalGrade != null)
            return Result<DefenseDto>.Failure("Cette soutenance a déjà été notée et ne peut plus être replanifiée.");

        if (string.IsNullOrWhiteSpace(command.Classroom))
            return Result<DefenseDto>.Failure("La salle est obligatoire.");

        if (command.DurationMinutes < MinDurationMinutes || command.DurationMinutes > MaxDurationMinutes)
            return Result<DefenseDto>.Failure($"La durée doit être comprise entre {MinDurationMinutes} et {MaxDurationMinutes} minutes.");

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
            internship.Defense?.Id,
            stopAtFirst: true,
            cancellationToken);

        if (conflicts.Count > 0)
            return Result<DefenseDto>.Failure(conflicts[0]);

        // Création ou replanification
        var defense = internship.Defense;
        if (defense == null)
        {
            defense = new Defense(command.InternshipId, command.ScheduledAt, classroom, command.DurationMinutes);
            await _context.Defenses.AddAsync(defense, cancellationToken);
        }
        else
        {
            defense.Reschedule(command.ScheduledAt, classroom, command.DurationMinutes);
            _context.DefenseJuryMembers.RemoveRange(defense.JuryMembers.ToList());
        }

        var newMembers = jury
            .Select(m => new DefenseJuryMember(defense.Id, m.TeacherId, m.TeacherFullName, m.Role))
            .ToList();
        await _context.DefenseJuryMembers.AddRangeAsync(newMembers, cancellationToken);

        if (internship.Status == InternshipStatus.EnCours)
        {
            var transition = internship.MarkDefenseScheduled(currentUserId);
            if (transition.IsFailure)
                return Result<DefenseDto>.Failure(transition.Error);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var names = await _identityService.GetUserNamesAsync(new[] { internship.StudentId }, cancellationToken);
        return Result<DefenseDto>.Success(MapToDto(defense, internship, names, newMembers));
    }

    public async Task<Result> RecordGradeAsync(Guid defenseId, RecordGradeCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);

        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        var grading = defense.RecordGrade(command.Grade, command.Comments, DateTime.UtcNow);
        if (grading.IsFailure)
            return grading;

        // Le stage doit être au statut « Soutenance planifiée » pour être clôturé.
        var completion = defense.Internship.Complete(currentUserId);
        if (completion.IsFailure)
            return completion;

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

    private static DefenseDto MapToDto(
        Defense d,
        Internship internship,
        IReadOnlyDictionary<string, string> names,
        IEnumerable<DefenseJuryMember>? juryMembers = null)
    {
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
                .ToList());
    }
}
