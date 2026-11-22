using InternTrack.Domain.Common;
using InternTrack.Domain.Enums;

namespace InternTrack.Domain.Entities;

public class DefenseJuryMember : BaseEntity
{
    public Guid DefenseId { get; private set; }
    public Defense Defense { get; private set; } = null!;

    public string TeacherId { get; private set; } = string.Empty;
    public string TeacherFullName { get; private set; } = string.Empty;
    public DefenseRole Role { get; private set; }

    private DefenseJuryMember() { }

    public DefenseJuryMember(Guid defenseId, string teacherId, string teacherFullName, DefenseRole role)
    {
        DefenseId = defenseId;
        TeacherId = teacherId ?? throw new ArgumentNullException(nameof(teacherId));
        TeacherFullName = teacherFullName ?? throw new ArgumentNullException(nameof(teacherFullName));
        Role = role;
    }
}

