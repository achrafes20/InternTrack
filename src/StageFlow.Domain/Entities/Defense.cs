using StageFlow.Domain.Common;
using StageFlow.Domain.Events;
using StageFlow.Domain.ValueObjects;

namespace StageFlow.Domain.Entities;

public class Defense : BaseAuditableEntity
{
    public DateTime ScheduledAt { get; private set; }

    /// <summary>
    /// Fin du créneau (ScheduledAt + durée), persistée pour permettre à PostgreSQL d'interdire
    /// physiquement deux soutenances qui se chevauchent dans la même salle (contrainte d'exclusion).
    /// </summary>
    public DateTime EndsAt { get; private set; }

    public int DurationMinutes { get; private set; } = 45;
    public string Classroom { get; private set; } = string.Empty;
    public decimal? FinalGrade { get; private set; }
    public string? Comments { get; private set; }
    public DateTime? GradedAt { get; private set; }
    public string? GradedBy { get; private set; }

    /// <summary>Motif obligatoire de la dernière correction de note (l'historique complet est dans le journal d'audit).</summary>
    public string? GradeCorrectionReason { get; private set; }
    public DateTime? GradeCorrectedAt { get; private set; }

    /// <summary>
    /// Lien d'évaluation envoyé au tuteur entreprise, membre externe du jury sans compte sur la plateforme.
    /// Seul le hash du jeton est conservé ; le lien reste utilisable (évaluation révisable) jusqu'à
    /// son expiration ou l'arrêt de la note finale.
    /// </summary>
    public string? TutorEvaluationTokenHash { get; private set; }
    public DateTime? TutorEvaluationTokenExpiresAt { get; private set; }

    public DefenseJuryMember? CompanyTutor => JuryMembers.FirstOrDefault(m => m.Role == Enums.DefenseRole.TuteurEntreprise);

    public Result<string> IssueTutorEvaluationToken(DateTime utcNow, TimeSpan validity)
    {
        if (CompanyTutor == null)
            return Result<string>.Failure("Aucun tuteur entreprise ne siège dans ce jury.");
        if (FinalGrade.HasValue)
            return Result<string>.Failure("La note finale est déjà arrêtée.");

        var rawToken = SecureToken.Generate();
        TutorEvaluationTokenHash = SecureToken.Hash(rawToken);
        TutorEvaluationTokenExpiresAt = utcNow.Add(validity);
        return Result<string>.Success(rawToken);
    }

    public bool IsTutorEvaluationTokenValid(string rawToken, DateTime utcNow)
        => TutorEvaluationTokenExpiresAt > utcNow && SecureToken.Matches(TutorEvaluationTokenHash, rawToken);

    public Result SubmitTutorEvaluation(string rawToken, decimal grade, string? comment, DateTime utcNow)
    {
        if (!IsTutorEvaluationTokenValid(rawToken, utcNow))
            return Result.Failure("Ce lien d'évaluation est invalide ou a expiré. Demandez un nouveau lien à l'école.");

        var tutor = CompanyTutor;
        if (tutor == null)
            return Result.Failure("Aucun tuteur entreprise ne siège dans ce jury.");

        return SubmitEvaluation(tutor.TeacherId, grade, comment, utcNow);
    }

    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public ICollection<DefenseJuryMember> JuryMembers { get; private set; } = new List<DefenseJuryMember>();
    public ICollection<DefenseEvaluation> Evaluations { get; private set; } = new List<DefenseEvaluation>();

    /// <summary>Moyenne des évaluations individuelles du jury, arrondie au centième (null si aucune).</summary>
    public decimal? AverageEvaluation => Evaluations.Count == 0
        ? null
        : decimal.Round(Evaluations.Average(e => e.Grade), 2, MidpointRounding.AwayFromZero);

    private Defense() { }

    public Defense(Guid internshipId, DateTime scheduledAt, string classroom, int durationMinutes = 45)
    {
        InternshipId = internshipId;
        SetSlot(scheduledAt, classroom, durationMinutes);

        AddDomainEvent(new DefenseScheduledEvent(Id, internshipId, scheduledAt, Classroom, DateTime.UtcNow));
    }

    public void Reschedule(DateTime scheduledAt, string classroom, int durationMinutes)
    {
        SetSlot(scheduledAt, classroom, durationMinutes);
        AddDomainEvent(new DefenseScheduledEvent(Id, InternshipId, scheduledAt, Classroom, DateTime.UtcNow));
    }

    private void SetSlot(DateTime scheduledAt, string classroom, int durationMinutes)
    {
        if (classroom is null) throw new ArgumentNullException(nameof(classroom));

        ScheduledAt = scheduledAt;
        Classroom = classroom.Trim();
        DurationMinutes = durationMinutes > 0 ? durationMinutes : 45;
        EndsAt = ScheduledAt.AddMinutes(DurationMinutes);
    }

    /// <summary>
    /// Enregistre la note finale. Une note ne peut être saisie qu'une fois la soutenance commencée,
    /// et une seule fois : une modification ultérieure passe par <see cref="CorrectGrade"/>.
    /// </summary>
    public Result RecordGrade(decimal grade, string? comments, DateTime utcNow, string gradedBy = "")
    {
        if (FinalGrade.HasValue)
            return Result.Failure("La note finale est déjà enregistrée. Utilisez la correction de note (motif obligatoire).");

        var validation = ValidateGrade(grade, comments, utcNow);
        if (validation.IsFailure)
            return validation;

        FinalGrade = grade;
        Comments = comments;
        GradedAt = utcNow;
        GradedBy = gradedBy;
        // Les évaluations sont closes : le lien du tuteur entreprise ne sert plus.
        TutorEvaluationTokenHash = null;
        TutorEvaluationTokenExpiresAt = null;
        return Result.Success();
    }

    /// <summary>Corrige une note déjà enregistrée (erreur de saisie, délibération). Le motif est obligatoire.</summary>
    public Result CorrectGrade(decimal grade, string? comments, string reason, DateTime utcNow, string correctedBy)
    {
        if (!FinalGrade.HasValue)
            return Result.Failure("Aucune note n'a encore été enregistrée pour cette soutenance.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Le motif de la correction est obligatoire.");

        if (reason.Length > FieldLimits.RejectionReason)
            return Result.Failure($"Le motif ne peut dépasser {FieldLimits.RejectionReason} caractères.");

        var validation = ValidateGrade(grade, comments, utcNow);
        if (validation.IsFailure)
            return validation;

        FinalGrade = grade;
        Comments = comments;
        GradeCorrectionReason = reason.Trim();
        GradeCorrectedAt = utcNow;
        GradedBy = correctedBy;
        return Result.Success();
    }

    /// <summary>
    /// Évaluation individuelle d'un membre interne du jury. Chaque enseignant peut réviser la sienne
    /// tant que la note finale n'est pas arrêtée.
    /// </summary>
    public Result SubmitEvaluation(string teacherId, decimal grade, string? comment, DateTime utcNow)
    {
        var member = JuryMembers.FirstOrDefault(m => m.TeacherId == teacherId);
        if (member == null)
            return Result.Failure("Seuls les membres du jury peuvent évaluer cette soutenance.");

        if (FinalGrade.HasValue)
            return Result.Failure("La note finale est déjà arrêtée : les évaluations ne peuvent plus être modifiées.");

        var validation = ValidateGrade(grade, comment, utcNow);
        if (validation.IsFailure)
            return validation;

        var existing = Evaluations.FirstOrDefault(e => e.TeacherId == teacherId);
        if (existing == null)
            Evaluations.Add(new DefenseEvaluation(Id, teacherId, member.TeacherFullName, grade, comment));
        else
            existing.Revise(grade, comment);

        return Result.Success();
    }

    public bool IsPresident(string teacherId)
        => JuryMembers.Any(m => m.TeacherId == teacherId && m.Role == Enums.DefenseRole.President);

    private Result ValidateGrade(decimal grade, string? comments, DateTime utcNow)
    {
        if (grade < 0 || grade > 20)
            return Result.Failure("La note doit être comprise entre 0 et 20.");

        if (decimal.Round(grade, 2) != grade)
            return Result.Failure("La note est limitée à deux décimales.");

        if (utcNow < ScheduledAt)
            return Result.Failure("La note ne peut être saisie qu'après le début de la soutenance.");

        if (comments is { Length: > FieldLimits.DefenseComments })
            return Result.Failure($"L'appréciation ne peut dépasser {FieldLimits.DefenseComments} caractères.");

        return Result.Success();
    }

    public DateRange GetTimeRange() => new(ScheduledAt, EndsAt);
}
