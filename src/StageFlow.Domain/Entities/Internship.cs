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
        StudentId = studentId ?? throw new ArgumentNullException(nameof(studentId));
        CompanyId = companyId;
        AcademicTrackId = academicTrackId;
        CompanySupervisorName = companySupervisorName ?? throw new ArgumentNullException(nameof(companySupervisorName));
        CompanySupervisorEmail = companySupervisorEmail ?? throw new ArgumentNullException(nameof(companySupervisorEmail));
        CompanySupervisorPhone = companySupervisorPhone;
        InternshipOfferId = internshipOfferId;

        Status = InternshipStatus.Brouillon;
    }

    public void UpdateDraft(
        string title,
        string subjectDescription,
        DateTime startDate,
        DateTime endDate,
        Guid companyId,
        string companySupervisorName,
        string companySupervisorEmail,
        string? companySupervisorPhone)
    {
        if (Status != InternshipStatus.Brouillon)
            throw new InvalidOperationException("Seul un dossier à l'état Brouillon peut être modifié.");

        var validation = DateRange.Validate(startDate, endDate);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Error);

        Title = title;
        SubjectDescription = subjectDescription;
        Period = new DateRange(startDate, endDate);
        CompanyId = companyId;
        CompanySupervisorName = companySupervisorName;
        CompanySupervisorEmail = companySupervisorEmail;
        CompanySupervisorPhone = companySupervisorPhone;
    }

    public void AssignAcademicSupervisor(string academicSupervisorId)
    {
        AcademicSupervisorId = academicSupervisorId ?? throw new ArgumentNullException(nameof(academicSupervisorId));
    }

    #region State Machine Workflow

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

