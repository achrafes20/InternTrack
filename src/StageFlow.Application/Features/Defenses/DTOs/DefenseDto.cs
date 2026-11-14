using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Defenses.DTOs;

public record DefenseDto(
    Guid Id,
    Guid InternshipId,
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
    public DateTime? DefenseDate { set { if (value.HasValue) ScheduledAt = value.Value; } }
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

public class JuryMemberInput
{
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherFullName { get; set; } = string.Empty;
    public string? TeacherName { set { if (!string.IsNullOrEmpty(value)) { TeacherFullName = value; if (string.IsNullOrEmpty(TeacherId)) TeacherId = value; } } }
    public DefenseRole Role { get; set; }

    public JuryMemberInput() { }

    public JuryMemberInput(string teacherId, string teacherFullName, DefenseRole role)
    {
        TeacherId = teacherId;
        TeacherFullName = teacherFullName;
        Role = role;
    }
}

public record CheckConflictRequest
{
    public DateTime DefenseDate { get; set; }
    public string Classroom { get; set; } = string.Empty;
    public List<string>? JuryTeacherNames { get; set; } = new();
    public List<string>? JuryTeacherIds { get; set; } = new();

    public CheckConflictRequest() { }

    public CheckConflictRequest(DateTime defenseDate, string classroom, List<string>? juryTeacherNames = null, List<string>? juryTeacherIds = null)
    {
        DefenseDate = defenseDate;
        Classroom = classroom;
        JuryTeacherNames = juryTeacherNames;
        JuryTeacherIds = juryTeacherIds;
    }

    [System.Text.Json.Serialization.JsonPropertyName("scheduledAt")]
    public DateTime? ScheduledAtAlias
    {
        set { if (value.HasValue && DefenseDate == default) DefenseDate = value.Value; }
    }
}

public record ConflictCheckResultDto(
    bool HasConflict,
    List<string> ConflictMessages);

public record RecordGradeCommand(
    decimal Grade,
    string? Comments);
