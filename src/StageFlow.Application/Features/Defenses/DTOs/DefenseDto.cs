using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Defenses.DTOs;

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
    List<JuryMemberDto> JuryMembers);

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

public record RecordGradeCommand(
    decimal Grade,
    string? Comments);

public record TeacherDto(string Id, string FullName, string Email, string Role, Guid? AcademicTrackId);
