using FluentValidation;
using InternTrack.Application.Features.Conventions.DTOs;
using InternTrack.Application.Features.Defenses.DTOs;
using InternTrack.Application.Features.Defenses.Services;
using InternTrack.Application.Features.InternshipOffers.DTOs;
using InternTrack.Application.Features.Internships.DTOs;
using InternTrack.Application.Features.ProgressReports.DTOs;
using InternTrack.Domain.Common;
using InternTrack.Domain.Enums;

namespace InternTrack.Application.Common.Validation;

/// <summary>Règles partagées (messages en français, limites alignées sur les colonnes de la base).</summary>
public static class ValidationRules
{
    private const string PhonePattern = @"^\+?[0-9 ().-]{6,30}$";

    public static IRuleBuilderOptions<T, string> RequiredText<T>(this IRuleBuilder<T, string> rule, string label, int maxLength)
        => rule
            .NotEmpty().WithMessage($"{label} est obligatoire.")
            .MaximumLength(maxLength).WithMessage($"{label} ne peut dépasser {maxLength} caractères.");

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, string label, int maxLength)
        => rule.MaximumLength(maxLength).WithMessage($"{label} ne peut dépasser {maxLength} caractères.");

    public static IRuleBuilderOptions<T, string> RequiredEmail<T>(this IRuleBuilder<T, string> rule, string label)
        => rule
            .NotEmpty().WithMessage($"{label} est obligatoire.")
            .MaximumLength(FieldLimits.Email).WithMessage($"{label} ne peut dépasser {FieldLimits.Email} caractères.")
            .EmailAddress().WithMessage($"{label} n'est pas une adresse email valide.");

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule, string label)
        => rule
            .MaximumLength(FieldLimits.Phone).WithMessage($"{label} ne peut dépasser {FieldLimits.Phone} caractères.")
            .Matches(PhonePattern).WithMessage($"{label} n'est pas un numéro de téléphone valide.");

    public static IRuleBuilderOptions<T, Guid> RequiredId<T>(this IRuleBuilder<T, Guid> rule, string label)
        => rule.NotEmpty().WithMessage($"{label} est obligatoire.");
}

public class ProposeInternshipCommandValidator : AbstractValidator<ProposeInternshipCommand>
{
    public ProposeInternshipCommandValidator()
    {
        RuleFor(c => c.Title).RequiredText("Le titre", FieldLimits.InternshipTitle);
        RuleFor(c => c.SubjectDescription).RequiredText("La description du sujet", FieldLimits.LongDescription);
        RuleFor(c => c.CompanyId).RequiredId("L'entreprise");
        RuleFor(c => c.AcademicTrackId).RequiredId("La filière");
        RuleFor(c => c.CompanySupervisorName).RequiredText("Le nom du tuteur entreprise", FieldLimits.PersonName);
        RuleFor(c => c.CompanySupervisorEmail).RequiredEmail("L'email du tuteur entreprise");
        RuleFor(c => c.CompanySupervisorPhone).OptionalPhone("Le téléphone du tuteur").When(c => !string.IsNullOrEmpty(c.CompanySupervisorPhone));
        RuleFor(c => c.StartDate).NotEmpty().WithMessage("La date de début est obligatoire.");
        RuleFor(c => c.EndDate).GreaterThan(c => c.StartDate).WithMessage("La date de fin doit être postérieure à la date de début.");
    }
}

public class UpdateInternshipDraftCommandValidator : AbstractValidator<UpdateInternshipDraftCommand>
{
    public UpdateInternshipDraftCommandValidator()
    {
        RuleFor(c => c.Title).RequiredText("Le titre", FieldLimits.InternshipTitle);
        RuleFor(c => c.SubjectDescription).RequiredText("La description du sujet", FieldLimits.LongDescription);
        RuleFor(c => c.CompanyId).RequiredId("L'entreprise");
        RuleFor(c => c.CompanySupervisorName).RequiredText("Le nom du tuteur entreprise", FieldLimits.PersonName);
        RuleFor(c => c.CompanySupervisorEmail).RequiredEmail("L'email du tuteur entreprise");
        RuleFor(c => c.CompanySupervisorPhone).OptionalPhone("Le téléphone du tuteur").When(c => !string.IsNullOrEmpty(c.CompanySupervisorPhone));
        RuleFor(c => c.StartDate).NotEmpty().WithMessage("La date de début est obligatoire.");
        RuleFor(c => c.EndDate).GreaterThan(c => c.StartDate).WithMessage("La date de fin doit être postérieure à la date de début.");
    }
}

public class RejectInternshipCommandValidator : AbstractValidator<RejectInternshipCommand>
{
    public RejectInternshipCommandValidator()
    {
        RuleFor(c => c.RejectionReason).RequiredText("Le motif de refus", FieldLimits.RejectionReason);
    }
}

public class CancelInternshipCommandValidator : AbstractValidator<CancelInternshipCommand>
{
    public CancelInternshipCommandValidator()
    {
        RuleFor(c => c.Reason).RequiredText("Le motif de l'annulation", FieldLimits.RejectionReason);
    }
}

public class CreateInternshipOfferCommandValidator : AbstractValidator<CreateInternshipOfferCommand>
{
    public CreateInternshipOfferCommandValidator()
    {
        RuleFor(c => c.Title).RequiredText("L'intitulé de l'offre", FieldLimits.InternshipTitle);
        RuleFor(c => c.Description).NotNull().WithMessage("La description est obligatoire.")
            .MaximumLength(FieldLimits.LongDescription).WithMessage($"La description ne peut dépasser {FieldLimits.LongDescription} caractères.");
        RuleFor(c => c.RequiredSkills).NotNull().WithMessage("Les compétences requises sont obligatoires (éventuellement vides).")
            .MaximumLength(FieldLimits.RequiredSkills).WithMessage($"Les compétences requises ne peuvent dépasser {FieldLimits.RequiredSkills} caractères.");
        RuleFor(c => c.City).RequiredText("La ville", FieldLimits.City);
        RuleFor(c => c.DurationInWeeks).InclusiveBetween(1, 52).WithMessage("La durée doit être comprise entre 1 et 52 semaines.");
        RuleFor(c => c.MonthlyStipend)
            .InclusiveBetween(0m, 99_999_999.99m).WithMessage("La gratification mensuelle est invalide.")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("La gratification est limitée à deux décimales.")
            .When(c => c.MonthlyStipend.HasValue);
        RuleFor(c => c.ExpirationDate).GreaterThan(_ => DateTime.UtcNow).WithMessage("La date d'expiration doit être dans le futur.");
        RuleFor(c => c.CompanyId).RequiredId("L'entreprise");
        RuleFor(c => c.AcademicTrackId).RequiredId("La filière");
    }
}

public class UpdateInternshipOfferCommandValidator : AbstractValidator<UpdateInternshipOfferCommand>
{
    public UpdateInternshipOfferCommandValidator()
    {
        RuleFor(c => c.Title).RequiredText("L'intitulé de l'offre", FieldLimits.InternshipTitle);
        RuleFor(c => c.Description).NotNull().WithMessage("La description est obligatoire.")
            .MaximumLength(FieldLimits.LongDescription).WithMessage($"La description ne peut dépasser {FieldLimits.LongDescription} caractères.");
        RuleFor(c => c.RequiredSkills).NotNull().WithMessage("Les compétences requises sont obligatoires (éventuellement vides).")
            .MaximumLength(FieldLimits.RequiredSkills).WithMessage($"Les compétences requises ne peuvent dépasser {FieldLimits.RequiredSkills} caractères.");
        RuleFor(c => c.City).RequiredText("La ville", FieldLimits.City);
        RuleFor(c => c.DurationInWeeks).InclusiveBetween(1, 52).WithMessage("La durée doit être comprise entre 1 et 52 semaines.");
        RuleFor(c => c.MonthlyStipend)
            .InclusiveBetween(0m, 99_999_999.99m).WithMessage("La gratification mensuelle est invalide.")
            .PrecisionScale(10, 2, ignoreTrailingZeros: true).WithMessage("La gratification est limitée à deux décimales.")
            .When(c => c.MonthlyStipend.HasValue);
        RuleFor(c => c.ExpirationDate).GreaterThan(_ => DateTime.UtcNow).WithMessage("La date d'expiration doit être dans le futur.");
    }
}

public class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(c => c.WeekNumber).InclusiveBetween(1, 60).WithMessage("Le numéro de semaine doit être compris entre 1 et 60.");
        RuleFor(c => c.Title).NotNull().WithMessage("Le titre ne peut pas être nul.")
            .MaximumLength(FieldLimits.ReportTitle).WithMessage($"Le titre ne peut dépasser {FieldLimits.ReportTitle} caractères.");
        RuleFor(c => c.ActivitiesPerformed).RequiredText("La description des activités", FieldLimits.ReportActivities);
        RuleFor(c => c.SkillsAcquired).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les compétences acquises ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
        RuleFor(c => c.DifficultiesEncountered).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les difficultés ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
        RuleFor(c => c.NextWeekGoals).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les objectifs ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
    }
}

public class UpdateReportCommandValidator : AbstractValidator<UpdateReportCommand>
{
    public UpdateReportCommandValidator()
    {
        RuleFor(c => c.Title).NotNull().MaximumLength(FieldLimits.ReportTitle)
            .WithMessage($"Le titre ne peut dépasser {FieldLimits.ReportTitle} caractères.");
        RuleFor(c => c.ActivitiesPerformed).RequiredText("La description des activités", FieldLimits.ReportActivities);
        RuleFor(c => c.SkillsAcquired).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les compétences acquises ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
        RuleFor(c => c.DifficultiesEncountered).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les difficultés ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
        RuleFor(c => c.NextWeekGoals).NotNull().MaximumLength(FieldLimits.ReportSection)
            .WithMessage($"Les objectifs ne peuvent dépasser {FieldLimits.ReportSection} caractères.");
    }
}

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(c => c.Content).RequiredText("Le commentaire", FieldLimits.Comment);
    }
}

public class JuryMemberInputValidator : AbstractValidator<JuryMemberInput>
{
    public JuryMemberInputValidator()
    {
        RuleFor(m => m.Role).IsInEnum().WithMessage("Rôle de jury invalide.");
        RuleFor(m => m.TeacherId).NotEmpty().WithMessage("Un membre interne du jury doit être choisi parmi les enseignants.")
            .MaximumLength(FieldLimits.UserId)
            .When(m => m.Role != DefenseRole.TuteurEntreprise);
        RuleFor(m => m.TeacherFullName).NotEmpty().WithMessage("Le nom du tuteur entreprise est obligatoire.")
            .When(m => m.Role == DefenseRole.TuteurEntreprise);
        RuleFor(m => m.TeacherFullName).OptionalText("Le nom du membre du jury", FieldLimits.PersonName);
    }
}

public class ScheduleDefenseCommandValidator : AbstractValidator<ScheduleDefenseCommand>
{
    public ScheduleDefenseCommandValidator()
    {
        RuleFor(c => c.InternshipId).RequiredId("Le stage");
        RuleFor(c => c.ScheduledAt).GreaterThan(_ => DateTime.UtcNow).WithMessage("La soutenance doit être planifiée dans le futur.");
        RuleFor(c => c.DurationMinutes).InclusiveBetween(DefenseService.MinDurationMinutes, DefenseService.MaxDurationMinutes)
            .WithMessage($"La durée doit être comprise entre {DefenseService.MinDurationMinutes} et {DefenseService.MaxDurationMinutes} minutes.");
        RuleFor(c => c.Classroom).RequiredText("La salle", FieldLimits.Classroom);
        RuleFor(c => c.JuryMembers).NotNull().Must(j => j.Count is >= 2 and <= 4)
            .WithMessage("Un jury doit comporter entre 2 et 4 membres.");
        RuleForEach(c => c.JuryMembers).SetValidator(new JuryMemberInputValidator());
    }
}

public class CheckConflictRequestValidator : AbstractValidator<CheckConflictRequest>
{
    public CheckConflictRequestValidator()
    {
        RuleFor(c => c.ScheduledAt).NotEmpty().WithMessage("La date de la soutenance est obligatoire.");
        RuleFor(c => c.DurationMinutes).InclusiveBetween(DefenseService.MinDurationMinutes, DefenseService.MaxDurationMinutes)
            .WithMessage($"La durée doit être comprise entre {DefenseService.MinDurationMinutes} et {DefenseService.MaxDurationMinutes} minutes.");
        RuleFor(c => c.Classroom).RequiredText("La salle", FieldLimits.Classroom);
        RuleFor(c => c.JuryTeacherIds).Must(ids => ids == null || ids.Count <= 4)
            .WithMessage("Un jury comporte au plus 4 membres.");
        RuleForEach(c => c.JuryTeacherIds).MaximumLength(FieldLimits.UserId);
    }
}

public class RecordGradeCommandValidator : AbstractValidator<RecordGradeCommand>
{
    public RecordGradeCommandValidator()
    {
        RuleFor(c => c.Comments).OptionalText("L'appréciation", FieldLimits.DefenseComments);
    }
}

public class CancelDefenseCommandValidator : AbstractValidator<CancelDefenseCommand>
{
    public CancelDefenseCommandValidator()
    {
        RuleFor(c => c.Reason).RequiredText("Le motif", FieldLimits.RejectionReason);
    }
}

public class SubmitEvaluationCommandValidator : AbstractValidator<SubmitEvaluationCommand>
{
    public SubmitEvaluationCommandValidator()
    {
        RuleFor(c => c.Grade).InclusiveBetween(0m, 20m).WithMessage("La note doit être comprise entre 0 et 20.")
            .PrecisionScale(4, 2, ignoreTrailingZeros: true).WithMessage("La note est limitée à deux décimales.");
        RuleFor(c => c.Comment).OptionalText("L'appréciation", FieldLimits.DefenseComments);
    }
}

public class CorrectGradeCommandValidator : AbstractValidator<CorrectGradeCommand>
{
    public CorrectGradeCommandValidator()
    {
        RuleFor(c => c.Grade).InclusiveBetween(0m, 20m).WithMessage("La note doit être comprise entre 0 et 20.")
            .PrecisionScale(4, 2, ignoreTrailingZeros: true).WithMessage("La note est limitée à deux décimales.");
        RuleFor(c => c.Comments).OptionalText("L'appréciation", FieldLimits.DefenseComments);
        RuleFor(c => c.Reason).RequiredText("Le motif de la correction", FieldLimits.RejectionReason);
    }
}

public class SignConventionCommandValidator : AbstractValidator<SignConventionCommand>
{
    public SignConventionCommandValidator()
    {
        RuleFor(c => c.Party).IsInEnum().WithMessage("Partie signataire invalide.");
    }
}

public class CreateConventionAmendmentCommandValidator : AbstractValidator<CreateConventionAmendmentCommand>
{
    public CreateConventionAmendmentCommandValidator()
    {
        RuleFor(c => c.Reason).RequiredText("Le motif de l'avenant", FieldLimits.RejectionReason);
        RuleFor(c => c.NewTitle).OptionalText("Le titre", FieldLimits.InternshipTitle);
        RuleFor(c => c.NewSubjectDescription).OptionalText("La description du sujet", FieldLimits.LongDescription);
        RuleFor(c => c.NewCompanySupervisorName).OptionalText("Le nom du tuteur", FieldLimits.PersonName);
        RuleFor(c => c.NewCompanySupervisorEmail)
            .MaximumLength(FieldLimits.Email).WithMessage($"L'e-mail du tuteur ne peut dépasser {FieldLimits.Email} caractères.")
            .EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.NewCompanySupervisorEmail))
            .WithMessage("L'e-mail du tuteur n'est pas une adresse email valide.");
        RuleFor(c => c.NewCompanySupervisorPhone).OptionalPhone("Le téléphone du tuteur")
            .When(c => !string.IsNullOrWhiteSpace(c.NewCompanySupervisorPhone));
        RuleFor(c => c).Must(c => !c.NewStartDate.HasValue || !c.NewEndDate.HasValue || c.NewEndDate > c.NewStartDate)
            .WithMessage("La date de fin doit être postérieure à la date de début.");
    }
}

public class SignConventionAmendmentCommandValidator : AbstractValidator<SignConventionAmendmentCommand>
{
    public SignConventionAmendmentCommandValidator()
    {
        RuleFor(c => c.Party).IsInEnum().WithMessage("Partie signataire invalide.");
        RuleFor(c => c.SignerName).OptionalText("Le nom du signataire", FieldLimits.PersonName);
    }
}

public class WithdrawConventionAmendmentCommandValidator : AbstractValidator<WithdrawConventionAmendmentCommand>
{
    public WithdrawConventionAmendmentCommandValidator()
    {
        RuleFor(c => c.Reason).RequiredText("Le motif du retrait", FieldLimits.RejectionReason);
    }
}
