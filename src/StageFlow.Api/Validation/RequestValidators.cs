using FluentValidation;
using StageFlow.Api.Controllers.v1;
using StageFlow.Application.Common.Validation;
using StageFlow.Domain.Common;
using StageFlow.Domain.Enums;

namespace StageFlow.Api.Validation;

internal static class KnownRoles
{
    public static readonly string[] All = { UserRole.Etudiant, UserRole.Encadrant, UserRole.ResponsableFiliere, UserRole.Admin };
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("L'email est obligatoire.")
            .MaximumLength(256).WithMessage("L'email est trop long.");
        // Borne la taille du mot de passe : évite de faire hacher (PBKDF2) des chaînes arbitrairement longues.
        RuleFor(r => r.Password).NotEmpty().WithMessage("Le mot de passe est obligatoire.")
            .MaximumLength(FieldLimits.Password).WithMessage("Le mot de passe est trop long.");
    }
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.Email).RequiredEmail("L'email");
        RuleFor(r => r.Password).NotEmpty().WithMessage("Le mot de passe est obligatoire.")
            .MaximumLength(FieldLimits.Password).WithMessage($"Le mot de passe ne peut dépasser {FieldLimits.Password} caractères.");
        RuleFor(r => r.FirstName).RequiredText("Le prénom", FieldLimits.FirstOrLastName);
        RuleFor(r => r.LastName).RequiredText("Le nom", FieldLimits.FirstOrLastName);
        RuleFor(r => r.Role).NotEmpty().Must(r => KnownRoles.All.Contains(r)).WithMessage("Rôle inconnu.");
        RuleFor(r => r.StudentNumber).OptionalText("Le code Apogée", FieldLimits.StudentNumber);
        RuleFor(r => r.AcademicTrackId).NotEmpty().WithMessage("Une filière est obligatoire pour ce rôle.")
            .When(r => r.Role is UserRole.Etudiant or UserRole.ResponsableFiliere);
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(r => r.Role).NotEmpty().Must(r => KnownRoles.All.Contains(r)).WithMessage("Rôle inconnu.");
    }
}

public class AssignSupervisorRequestValidator : AbstractValidator<AssignSupervisorRequest>
{
    public AssignSupervisorRequestValidator()
    {
        RuleFor(r => r.SupervisorId).RequiredText("L'identifiant de l'encadrant", FieldLimits.UserId);
    }
}

public class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(r => r.Name).RequiredText("Le nom de l'entreprise", FieldLimits.CompanyName);
        RuleFor(r => r.Industry).OptionalText("Le secteur", FieldLimits.Industry);
        RuleFor(r => r.City).RequiredText("La ville", FieldLimits.City);
        RuleFor(r => r.Address).OptionalText("L'adresse", FieldLimits.Address);
        RuleFor(r => r.ContactEmail).RequiredEmail("L'email de contact");
        RuleFor(r => r.ContactPhone).OptionalPhone("Le téléphone").When(r => !string.IsNullOrEmpty(r.ContactPhone));
        RuleFor(r => r.Website)
            .MaximumLength(FieldLimits.Website).WithMessage($"Le site web ne peut dépasser {FieldLimits.Website} caractères.")
            .Must(BeHttpUrl).WithMessage("Le site web doit être une adresse http(s) valide.")
            .When(r => !string.IsNullOrEmpty(r.Website));
    }

    private static bool BeHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
