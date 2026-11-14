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
}

public class DefenseService : IDefenseService
{
    private readonly IApplicationDbContext _context;

    public DefenseService(IApplicationDbContext context)
    {
        _context = context;
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

        return defenses.Select(d => MapToDto(d)).ToList();
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

        return Result<DefenseDto>.Success(MapToDto(defense));
    }

    public async Task<ConflictCheckResultDto> CheckConflictAsync(CheckConflictRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        var proposedRange = new DateRange(request.DefenseDate, request.DefenseDate.AddMinutes(45));

        // 1. Conflit de salle
        var roomDefenses = await _context.Defenses
            .Where(d => d.Classroom.ToLower() == request.Classroom.ToLower())
            .ToListAsync(cancellationToken);

        if (roomDefenses.Any(d => d.GetTimeRange().OverlapsWith(proposedRange)))
        {
            messages.Add($"La salle '{request.Classroom}' est déjà réservée sur ce créneau horaire.");
        }

        // 2. Conflit d'enseignants (comparaison par identifiant ou par nom)
        var teacherKeys = BuildTeacherKeys(request.JuryTeacherNames, request.JuryTeacherIds);
        if (teacherKeys.Count > 0)
        {
            var allDefenses = await _context.Defenses
                .Include(d => d.JuryMembers)
                .ToListAsync(cancellationToken);

            foreach (var def in allDefenses)
            {
                if (def.GetTimeRange().OverlapsWith(proposedRange))
                {
                    var conflict = def.JuryMembers.FirstOrDefault(m => MatchesTeacher(m, teacherKeys));
                    if (conflict != null)
                    {
                        messages.Add($"L'enseignant '{conflict.TeacherFullName}' est déjà membre d'un jury sur ce créneau horaire.");
                    }
                }
            }
        }

        return new ConflictCheckResultDto(messages.Count > 0, messages);
    }

    public async Task<Result<DefenseDto>> ScheduleDefenseAsync(ScheduleDefenseCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var internship = await _context.Internships
            .Include(i => i.Defense)
            .FirstOrDefaultAsync(i => i.Id == command.InternshipId, cancellationToken);

        if (internship == null)
            return Result<DefenseDto>.Failure("Stage introuvable.");

        if (internship.Status != InternshipStatus.EnCours && internship.Status != InternshipStatus.SoutenancePlanifiee)
            return Result<DefenseDto>.Failure($"La soutenance ne peut être planifiée que pour un stage en cours (Statut : {internship.Status}).");

        if (command.JuryMembers == null || command.JuryMembers.Count < 2 || command.JuryMembers.Count > 4)
            return Result<DefenseDto>.Failure("Un jury doit comporter entre 2 et 4 membres.");

        var proposedRange = new DateRange(command.ScheduledAt, command.ScheduledAt.AddMinutes(command.DurationMinutes));

        // 1. Détection de conflit de salle (Classroom collision)
        var roomConflict = await _context.Defenses
            .Where(d => d.Id != (internship.Defense != null ? internship.Defense.Id : Guid.Empty) &&
                        d.Classroom.ToLower() == command.Classroom.ToLower())
            .ToListAsync(cancellationToken);

        var hasRoomConflict = roomConflict.Any(d => d.GetTimeRange().OverlapsWith(proposedRange));
        if (hasRoomConflict)
            return Result<DefenseDto>.Failure($"La salle '{command.Classroom}' est déjà réservée sur ce créneau horaire.");

        // 2. Détection de conflit de membres de jury (identifiant ou nom de l'enseignant)
        var teacherKeys = BuildTeacherKeys(
            command.JuryMembers.Select(j => j.TeacherFullName),
            command.JuryMembers.Select(j => j.TeacherId));

        var existingDefenses = await _context.Defenses
            .Include(d => d.JuryMembers)
            .Where(d => d.Id != (internship.Defense != null ? internship.Defense.Id : Guid.Empty))
            .ToListAsync(cancellationToken);

        foreach (var def in existingDefenses)
        {
            if (def.GetTimeRange().OverlapsWith(proposedRange))
            {
                var conflictingTeacher = def.JuryMembers.FirstOrDefault(m => MatchesTeacher(m, teacherKeys));
                if (conflictingTeacher != null)
                {
                    return Result<DefenseDto>.Failure($"L'enseignant '{conflictingTeacher.TeacherFullName}' fait déjà partie d'un jury sur ce créneau horaire.");
                }
            }
        }

        // Création ou replanification
        var defense = internship.Defense;
        if (defense == null)
        {
            defense = new Defense(command.InternshipId, command.ScheduledAt, command.Classroom, command.DurationMinutes);
            await _context.Defenses.AddAsync(defense, cancellationToken);
        }
        else
        {
            defense.Reschedule(command.ScheduledAt, command.Classroom);
            var oldMembers = await _context.DefenseJuryMembers.Where(m => m.DefenseId == defense.Id).ToListAsync(cancellationToken);
            _context.DefenseJuryMembers.RemoveRange(oldMembers);
        }

        foreach (var memberInput in command.JuryMembers)
        {
            var juryMember = new DefenseJuryMember(
                defense.Id,
                string.IsNullOrEmpty(memberInput.TeacherId) ? memberInput.TeacherFullName : memberInput.TeacherId,
                memberInput.TeacherFullName,
                memberInput.Role);
            await _context.DefenseJuryMembers.AddAsync(juryMember, cancellationToken);
        }

        // Mise à jour du statut du stage
        if (internship.Status == InternshipStatus.EnCours)
        {
            internship.MarkDefenseScheduled(currentUserId);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<DefenseDto>.Success(MapToDto(defense, internship.Title));
    }

    public async Task<Result> RecordGradeAsync(Guid defenseId, RecordGradeCommand command, string currentUserId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.Internship)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);

        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        // Le stage doit être au statut « Soutenance planifiée » pour être clôturé.
        var completion = defense.Internship.Complete(currentUserId);
        if (completion.IsFailure)
            return completion;

        try
        {
            defense.RecordGrade(command.Grade, command.Comments);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static HashSet<string> BuildTeacherKeys(IEnumerable<string?>? names, IEnumerable<string?>? ids)
    {
        return (names ?? Enumerable.Empty<string?>())
            .Concat(ids ?? Enumerable.Empty<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .ToHashSet();
    }

    private static bool MatchesTeacher(DefenseJuryMember member, HashSet<string> teacherKeys)
    {
        if (!string.IsNullOrWhiteSpace(member.TeacherId) &&
            teacherKeys.Contains(member.TeacherId.Trim().ToLowerInvariant()))
            return true;

        return !string.IsNullOrWhiteSpace(member.TeacherFullName) &&
               teacherKeys.Contains(member.TeacherFullName.Trim().ToLowerInvariant());
    }

    private static DefenseDto MapToDto(Defense d, string? internshipTitle = null)
    {
        return new DefenseDto(
            d.Id,
            d.InternshipId,
            d.Internship?.StudentId ?? "Étudiant",
            internshipTitle ?? d.Internship?.Title ?? "Stage",
            d.ScheduledAt,
            d.DurationMinutes,
            d.Classroom,
            d.FinalGrade,
            d.Comments,
            d.JuryMembers.Select(m => new JuryMemberDto(m.Id, m.TeacherId, m.TeacherFullName, m.Role, m.Role.ToString())).ToList()
        );
    }
}
