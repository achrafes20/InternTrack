using StageFlow.Domain.Common;

namespace StageFlow.Domain.Entities;

/// <summary>
/// Évaluation individuelle d'un membre interne du jury (note proposée et appréciation).
/// La note finale est ensuite arrêtée par le président du jury ou le responsable de filière,
/// en s'appuyant sur la moyenne des évaluations.
/// </summary>
public class DefenseEvaluation : BaseAuditableEntity
{
    public Guid DefenseId { get; private set; }
    public Defense Defense { get; private set; } = null!;

    public string TeacherId { get; private set; } = string.Empty;
    public string TeacherFullName { get; private set; } = string.Empty;
    public decimal Grade { get; private set; }
    public string? Comment { get; private set; }

    private DefenseEvaluation() { }

    internal DefenseEvaluation(Guid defenseId, string teacherId, string teacherFullName, decimal grade, string? comment)
    {
        DefenseId = defenseId;
        TeacherId = teacherId ?? throw new ArgumentNullException(nameof(teacherId));
        TeacherFullName = FieldLimits.Truncate(teacherFullName ?? throw new ArgumentNullException(nameof(teacherFullName)), FieldLimits.PersonName);
        Grade = grade;
        Comment = comment;
    }

    internal void Revise(decimal grade, string? comment)
    {
        Grade = grade;
        Comment = comment;
    }
}
