using InternTrack.Domain.Enums;

namespace InternTrack.Application.Features.Defenses.DTOs;

public record DefenseDto(
    Guid Id,
    Guid InternshipId,
    string StudentId,
    string StudentName,
    string InternshipTitle,
    DateTime ScheduledAt,
    int DurationMinutes,
    string Classroom,
    decimal? FinalGrade,
    string? Comments,
    List<JuryMemberDto> JuryMembers,
    List<DefenseEvaluationDto>? Evaluations = null,
    decimal? AverageEvaluation = null,
    DateTime? GradedAt = null,
    string? GradeCorrectionReason = null,
    DateTime? GradeCorrectedAt = null)
{
    /// <summary>Session : 1 = normale, 2 = rattrapage.</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>Note de la session précédente (rattrapage).</summary>
    public decimal? PreviousAttemptGrade { get; init; }

    /// <summary>Soutenance reportée (créneau libéré) et motif.</summary>
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public string InternshipStatus { get; init; } = string.Empty;

    /// <summary>Résultat au regard du seuil de validation (null tant que la note n'est pas arrêtée).</summary>
    public bool? Passed { get; init; }
    public decimal PassingGrade { get; init; }

    /// <summary>Part de la note du tuteur entreprise dans la note finale.</summary>
    public decimal TutorWeight { get; init; }

    /// <summary>Note qui serait arrêtée avec les évaluations actuelles (null si des évaluations manquent).</summary>
    public decimal? ProvisionalGrade { get; init; }

    /// <summary>Membres du jury n'ayant pas encore évalué.</summary>
    public List<string> MissingEvaluations { get; init; } = new();
}

public record DefenseEvaluationDto(
    string TeacherId,
    string TeacherFullName,
    decimal Grade,
    string? Comment,
    DateTime SubmittedAt);

public record SubmitEvaluationCommand(decimal Grade, string? Comment);

public record CorrectGradeCommand(decimal Grade, string? Comments, string Reason);

public record JuryMemberDto(
    Guid Id,
    string TeacherId,
    string TeacherFullName,
    DefenseRole Role,
    string RoleName);

public class ScheduleDefenseCommand
{
    public Guid InternshipId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 45;
    public string Classroom { get; set; } = string.Empty;
    public List<JuryMemberInput> JuryMembers { get; set; } = new();

    public ScheduleDefenseCommand() { }

    public ScheduleDefenseCommand(
        Guid internshipId,
        DateTime scheduledAt,
        int durationMinutes,
        string classroom,
        List<JuryMemberInput> juryMembers)
    {
        InternshipId = internshipId;
        ScheduledAt = scheduledAt;
        DurationMinutes = durationMinutes;
        Classroom = classroom;
        JuryMembers = juryMembers;
    }
}

/// <summary>
/// Membre du jury. Les membres internes (président, rapporteur, examinateur) sont identifiés
/// par l'identifiant de leur compte enseignant. Le tuteur entreprise est externe : seul son nom est requis.
/// </summary>
public class JuryMemberInput
{
    public string? TeacherId { get; set; }
    public string? TeacherFullName { get; set; }
    public DefenseRole Role { get; set; }

    public JuryMemberInput() { }

    public JuryMemberInput(string? teacherId, string? teacherFullName, DefenseRole role)
    {
        TeacherId = teacherId;
        TeacherFullName = teacherFullName;
        Role = role;
    }
}

public class CheckConflictRequest
{
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 45;
    public string Classroom { get; set; } = string.Empty;
    public List<string> JuryTeacherIds { get; set; } = new();

    /// <summary>Stage concerné : sa soutenance existante est ignorée lors d'une replanification.</summary>
    public Guid? InternshipId { get; set; }
}

public record ConflictCheckResultDto(
    bool HasConflict,
    List<string> ConflictMessages);

/// <summary>
/// Arrêt de la note finale : la note est calculée à partir des évaluations du jury (pas de saisie libre) ;
/// seule l'appréciation générale est fournie.
/// </summary>
public record RecordGradeCommand(string? Comments);

public record CancelDefenseCommand(string Reason);

public record TeacherDto(string Id, string FullName, string Email, string Role, Guid? AcademicTrackId);
