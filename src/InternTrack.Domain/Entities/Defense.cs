using InternTrack.Domain.Common;
using InternTrack.Domain.Events;
using InternTrack.Domain.ValueObjects;

namespace InternTrack.Domain.Entities;

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

    /// <summary>Session de soutenance : 1 pour la session normale, 2 (ou plus) pour un rattrapage.</summary>
    public int Attempt { get; private set; } = 1;

    /// <summary>Note obtenue à la session précédente (conservée lors d'un rattrapage).</summary>
    public decimal? PreviousAttemptGrade { get; private set; }
    public DateTime? PreviousAttemptGradedAt { get; private set; }

    /// <summary>
    /// Report d'une soutenance non notée (absence, maladie, mémoire non prêt). Le créneau est libéré :
    /// une soutenance annulée n'entre plus dans les conflits de salle ni de jury.
    /// </summary>
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public bool IsCancelled => CancelledAt.HasValue;
    public bool IsRetake => Attempt > 1;

    public DefenseJuryMember? CompanyTutor => JuryMembers.FirstOrDefault(m => m.Role == Enums.DefenseRole.TuteurEntreprise);

    public Result<string> IssueTutorEvaluationToken(DateTime utcNow, TimeSpan validity)
    {
        if (CompanyTutor == null)
            return Result<string>.Failure("Aucun tuteur entreprise ne siège dans ce jury.");
        if (FinalGrade.HasValue)
            return Result<string>.Failure("La note finale est déjà arrêtée.");
        if (IsCancelled)
            return Result<string>.Failure("Cette soutenance a été annulée.");

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

    /// <summary>Moyenne simple des évaluations individuelles, arrondie au centième (null si aucune). Indicatif : la note finale est calculée par <see cref="ComputeFinalGrade"/>.</summary>
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

    /// <summary>Nouveau créneau pour une soutenance non notée (réactive une soutenance reportée).</summary>
    public Result Reschedule(DateTime scheduledAt, string classroom, int durationMinutes)
    {
        if (FinalGrade.HasValue)
            return Result.Failure("Cette soutenance a déjà été notée et ne peut plus être replanifiée.");

        SetSlot(scheduledAt, classroom, durationMinutes);
        CancelledAt = null;
        CancellationReason = null;
        AddDomainEvent(new DefenseScheduledEvent(Id, InternshipId, scheduledAt, Classroom, DateTime.UtcNow));
        return Result.Success();
    }

    /// <summary>
    /// Ouvre une session de rattrapage après un échec : la note précédente est conservée, les évaluations
    /// de la session précédente sont écartées par le service, et le jury évalue à nouveau.
    /// </summary>
    public Result StartRetake(DateTime scheduledAt, string classroom, int durationMinutes, int maxAttempts)
    {
        if (!FinalGrade.HasValue)
            return Result.Failure("La session en cours n'a pas encore été notée.");
        if (Attempt >= maxAttempts)
            return Result.Failure($"Le nombre maximal de sessions ({maxAttempts}) est atteint : aucun rattrapage n'est possible.");

        PreviousAttemptGrade = FinalGrade;
        PreviousAttemptGradedAt = GradedAt;
        Attempt++;
        FinalGrade = null;
        Comments = null;
        GradedAt = null;
        GradedBy = null;
        GradeCorrectionReason = null;
        GradeCorrectedAt = null;
        Evaluations.Clear();

        SetSlot(scheduledAt, classroom, durationMinutes);
        CancelledAt = null;
        CancellationReason = null;
        AddDomainEvent(new DefenseScheduledEvent(Id, InternshipId, scheduledAt, Classroom, DateTime.UtcNow));
        return Result.Success();
    }

    /// <summary>Reporte une soutenance non notée : le créneau, la salle et le jury sont libérés.</summary>
    public Result Cancel(string reason, DateTime utcNow)
    {
        if (FinalGrade.HasValue)
            return Result.Failure("Une soutenance déjà notée ne peut pas être annulée : utilisez la correction de note.");
        if (IsCancelled)
            return Result.Failure("Cette soutenance est déjà annulée.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Le motif de l'annulation est obligatoire.");
        if (reason.Length > FieldLimits.RejectionReason)
            return Result.Failure($"Le motif ne peut dépasser {FieldLimits.RejectionReason} caractères.");

        CancelledAt = utcNow;
        CancellationReason = reason.Trim();
        TutorEvaluationTokenHash = null;
        TutorEvaluationTokenExpiresAt = null;
        return Result.Success();
    }

    /// <summary>
    /// Calcule la note finale à partir des évaluations : moyenne des membres internes du jury, pondérée
    /// avec la note du tuteur entreprise lorsqu'il a évalué. Toutes les évaluations internes sont exigées,
    /// celle du tuteur seulement si <paramref name="requireTutorEvaluation"/>.
    /// </summary>
    public Result<decimal> ComputeFinalGrade(decimal tutorWeight, bool requireTutorEvaluation)
    {
        if (tutorWeight is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(tutorWeight), "La pondération du tuteur doit être comprise entre 0 et 1.");

        var internalMembers = JuryMembers.Where(m => m.Role != Enums.DefenseRole.TuteurEntreprise).ToList();
        var missing = internalMembers
            .Where(m => Evaluations.All(e => e.TeacherId != m.TeacherId))
            .Select(m => m.TeacherFullName)
            .ToList();
        if (missing.Count > 0)
            return Result<decimal>.Failure($"Évaluation manquante : {string.Join(", ", missing)}. Chaque membre du jury doit évaluer avant l'arrêt de la note.");

        var internalAverage = Evaluations
            .Where(e => internalMembers.Any(m => m.TeacherId == e.TeacherId))
            .Average(e => e.Grade);

        var tutor = CompanyTutor;
        var tutorEvaluation = tutor == null ? null : Evaluations.FirstOrDefault(e => e.TeacherId == tutor.TeacherId);
        if (tutor != null && tutorEvaluation == null && requireTutorEvaluation)
            return Result<decimal>.Failure("Le tuteur entreprise n'a pas encore rendu son évaluation.");

        var grade = tutorEvaluation == null
            ? internalAverage
            : internalAverage * (1 - tutorWeight) + tutorEvaluation.Grade * tutorWeight;

        return Result<decimal>.Success(decimal.Round(grade, 2, MidpointRounding.AwayFromZero));
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
        if (IsCancelled)
            return Result.Failure("Cette soutenance a été annulée : replanifiez-la avant toute évaluation.");

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
