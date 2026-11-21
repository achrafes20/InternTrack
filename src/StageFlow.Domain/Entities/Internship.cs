using StageFlow.Domain.Common;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;
using StageFlow.Domain.ValueObjects;

namespace StageFlow.Domain.Entities;

public class Internship : BaseAuditableEntity
{
    public string Title { get; private set; } = string.Empty;
    public string SubjectDescription { get; private set; } = string.Empty;
    public DateRange Period { get; private set; } = null!;
    public InternshipStatus Status { get; private set; } = InternshipStatus.Brouillon;
    public string? RejectionReason { get; private set; }

    /// <summary>Motif d'une annulation administrative (statut Annulé).</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Année universitaire du stage (ex. « 2026-2027 »), déduite de la date de début :
    /// l'année commence le 1er septembre. Persistée pour filtrer, comparer et archiver par année.
    /// </summary>
    public string AcademicYear { get; private set; } = string.Empty;

    public const int AcademicYearStartMonth = 9;

    public static string ComputeAcademicYear(DateTime startDate)
    {
        var firstYear = startDate.Month >= AcademicYearStartMonth ? startDate.Year : startDate.Year - 1;
        return $"{firstYear}-{firstYear + 1}";
    }

    // Tuteur entreprise
    public string CompanySupervisorName { get; private set; } = string.Empty;
    public string CompanySupervisorEmail { get; private set; } = string.Empty;
    public string? CompanySupervisorPhone { get; private set; }

    // Clés étrangères
    public string StudentId { get; private set; } = string.Empty;
    public string? AcademicSupervisorId { get; private set; } // Enseignant encadrant

    public Guid CompanyId { get; private set; }
    public Company Company { get; private set; } = null!;

    public Guid AcademicTrackId { get; private set; }
    public AcademicTrack AcademicTrack { get; private set; } = null!;

    public Guid? InternshipOfferId { get; private set; } // Null si stage spontané
    public InternshipOffer? InternshipOffer { get; private set; }

    // Relations 1-1 & 1-N
    public Convention? Convention { get; private set; }
    public Defense? Defense { get; private set; }
    public ICollection<ProgressReport> Reports { get; private set; } = new List<ProgressReport>();
    public ICollection<InternshipDocument> Documents { get; private set; } = new List<InternshipDocument>();

    /// <summary>
    /// Statuts d'un dossier « engagé » : un élève ne peut en avoir qu'un seul à la fois
    /// (les brouillons, dossiers refusés et stages terminés ne comptent pas).
    /// </summary>
    public static readonly InternshipStatus[] EngagedStatuses =
    {
        InternshipStatus.Propose,
        InternshipStatus.Valide,
        InternshipStatus.EnCours,
        InternshipStatus.SoutenancePlanifiee
    };

    /// <summary>Statuts pendant lesquels le stage se déroule effectivement (rapports, mémoire).</summary>
    public static readonly InternshipStatus[] RunningStatuses =
    {
        InternshipStatus.EnCours,
        InternshipStatus.SoutenancePlanifiee
    };

    /// <summary>Nombre de semaines couvertes par la période (la dernière semaine entamée compte).</summary>
    public int TotalWeeks => Math.Max(1, (int)Math.Ceiling((Period.EndDate - Period.StartDate).TotalDays / 7.0));

    /// <summary>Premier jour de la semaine n°<paramref name="weekNumber"/> (1 = semaine de début du stage).</summary>
    public DateTime WeekStart(int weekNumber) => Period.StartDate.AddDays((weekNumber - 1) * 7);

    private Internship() { }

    public Internship(
        string title,
        string subjectDescription,
        DateTime startDate,
        DateTime endDate,
        string studentId,
        Guid companyId,
        Guid academicTrackId,
        string companySupervisorName,
        string companySupervisorEmail,
        string? companySupervisorPhone = null,
        Guid? internshipOfferId = null)
    {
        var validation = DateRange.Validate(startDate, endDate);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Error);

        Title = title ?? throw new ArgumentNullException(nameof(title));
        SubjectDescription = subjectDescription ?? throw new ArgumentNullException(nameof(subjectDescription));
        Period = new DateRange(startDate, endDate);
        AcademicYear = ComputeAcademicYear(startDate);
        StudentId = studentId ?? throw new ArgumentNullException(nameof(studentId));
        CompanyId = companyId;
        AcademicTrackId = academicTrackId;
        CompanySupervisorName = companySupervisorName ?? throw new ArgumentNullException(nameof(companySupervisorName));
        CompanySupervisorEmail = companySupervisorEmail ?? throw new ArgumentNullException(nameof(companySupervisorEmail));
        CompanySupervisorPhone = companySupervisorPhone;
        InternshipOfferId = internshipOfferId;

        Status = InternshipStatus.Brouillon;
    }

    public Result UpdateDraft(
        string title,
        string subjectDescription,
        DateTime startDate,
        DateTime endDate,
        Guid companyId,
        string companySupervisorName,
        string companySupervisorEmail,
        string? companySupervisorPhone,
        string currentUserId)
    {
        if (currentUserId != StudentId)
            return Result.Failure("Seul l'étudiant propriétaire peut modifier ce dossier.");

        if (Status != InternshipStatus.Brouillon)
            return Result.Failure("Seul un dossier à l'état Brouillon peut être modifié.");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(subjectDescription))
            return Result.Failure("Le titre et la description du sujet sont obligatoires.");

        if (string.IsNullOrWhiteSpace(companySupervisorName) || string.IsNullOrWhiteSpace(companySupervisorEmail))
            return Result.Failure("Le nom et l'email du tuteur entreprise sont obligatoires.");

        var validation = DateRange.Validate(startDate, endDate);
        if (!validation.IsValid)
            return Result.Failure(validation.Error!);

        Title = title;
        SubjectDescription = subjectDescription;
        Period = new DateRange(startDate, endDate);
        AcademicYear = ComputeAcademicYear(startDate);
        CompanyId = companyId;
        CompanySupervisorName = companySupervisorName;
        CompanySupervisorEmail = companySupervisorEmail;
        CompanySupervisorPhone = companySupervisorPhone;
        return Result.Success();
    }

    public void AssignAcademicSupervisor(string academicSupervisorId)
    {
        AcademicSupervisorId = academicSupervisorId ?? throw new ArgumentNullException(nameof(academicSupervisorId));
    }

    #region State Machine Workflow

    /// <summary>
    /// Annulation administrative (abandon, doublon, dossier incohérent). Interdite une fois la soutenance
    /// planifiée ou le stage terminé : ces dossiers relèvent d'une décision pédagogique (note, jury).
    /// </summary>
    public Result Cancel(string currentUserId, string reason)
    {
        if (Status is InternshipStatus.SoutenancePlanifiee or InternshipStatus.Termine or InternshipStatus.Annule or InternshipStatus.Refuse)
            return Result.Failure($"Un dossier au statut '{Status}' ne peut pas être annulé.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Le motif de l'annulation est obligatoire.");

        if (reason.Length > FieldLimits.RejectionReason)
            return Result.Failure($"Le motif ne peut dépasser {FieldLimits.RejectionReason} caractères.");

        var prev = Status;
        Status = InternshipStatus.Annule;
        CancellationReason = reason.Trim();
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    /// <summary>
    /// Après un refus, l'étudiant propriétaire remet son dossier en brouillon pour le corriger
    /// puis le soumettre à nouveau. Le motif de refus reste visible jusqu'à la prochaine validation.
    /// </summary>
    public Result ReopenAsDraft(string currentUserId)
    {
        if (currentUserId != StudentId)
            return Result.Failure("Seul l'étudiant propriétaire peut retravailler ce dossier.");

        if (Status != InternshipStatus.Refuse)
            return Result.Failure($"Seul un dossier refusé peut être remis en brouillon (Statut actuel : {Status}).");

        var prev = Status;
        Status = InternshipStatus.Brouillon;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result SubmitForApproval(string currentUserId)
    {
        if (currentUserId != StudentId)
            return Result.Failure("Seul l'étudiant propriétaire peut soumettre ce dossier.");

        if (Status != InternshipStatus.Brouillon)
            return Result.Failure($"Impossible de soumettre un dossier ayant le statut '{Status}'. Il doit être au statut 'Brouillon'.");

        var prev = Status;
        Status = InternshipStatus.Propose;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result Approve(string currentUserId)
    {
        if (Status != InternshipStatus.Propose)
            return Result.Failure($"Impossible de valider un dossier ayant le statut '{Status}'. Il doit être au statut 'Propose'.");

        var prev = Status;
        Status = InternshipStatus.Valide;
        RejectionReason = null;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result Reject(string currentUserId, string rejectionReason)
    {
        if (Status != InternshipStatus.Propose)
            return Result.Failure($"Impossible de refuser un dossier ayant le statut '{Status}'. Il doit être au statut 'Propose'.");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            return Result.Failure("Un motif explicite est obligatoire pour refuser un dossier.");

        var prev = Status;
        Status = InternshipStatus.Refuse;
        RejectionReason = rejectionReason;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result StartInternship(string currentUserId)
    {
        if (Status != InternshipStatus.Valide)
            return Result.Failure($"Le stage ne peut démarrer que s'il est validé (Statut actuel : {Status}).");

        var prev = Status;
        Status = InternshipStatus.EnCours;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result MarkDefenseScheduled(string currentUserId)
    {
        if (Status != InternshipStatus.EnCours)
            return Result.Failure($"Une soutenance ne peut être planifiée que pour un stage en cours (Statut actuel : {Status}).");

        var prev = Status;
        Status = InternshipStatus.SoutenancePlanifiee;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    public Result Complete(string currentUserId)
    {
        if (Status != InternshipStatus.SoutenancePlanifiee)
            return Result.Failure($"Le stage ne peut être terminé que si sa soutenance a eu lieu (Statut actuel : {Status}).");

        var prev = Status;
        Status = InternshipStatus.Termine;
        AddDomainEvent(new InternshipStatusChangedEvent(Id, prev, Status, currentUserId, DateTime.UtcNow));
        return Result.Success();
    }

    #endregion
}

