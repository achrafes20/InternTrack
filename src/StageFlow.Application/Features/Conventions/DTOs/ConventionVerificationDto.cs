using StageFlow.Domain.Entities;

namespace StageFlow.Application.Features.Conventions.DTOs;

public record ConventionVerificationDto(
    string ConventionNumber,
    string StudentFullName,
    string AcademicTrack,
    string CompanyName,
    DateTime StartDate,
    DateTime EndDate,
    bool IsAuthentic,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    bool IsFullySigned,
    DateTime? SignedAt,
    string? SignedDocumentSha256 = null);

public record SignConventionCommand(ConventionParty Party);

/// <summary>Enregistrement de la signature entreprise par l'école, à réception de l'exemplaire papier signé.</summary>
public record RecordCompanySignatureCommand(string SignerName);

/// <summary>Signature par l'entreprise via le lien reçu par e-mail.</summary>
public record CompanySignCommand(string SignerName, bool AcceptTerms);

/// <summary>Informations présentées à l'entreprise sur la page publique de signature (minimum nécessaire).</summary>
public record CompanySigningInfoDto(
    string ConventionNumber,
    string InternshipTitle,
    string SubjectDescription,
    string StudentFullName,
    string AcademicTrack,
    string CompanyName,
    string CompanySupervisorName,
    DateTime StartDate,
    DateTime EndDate,
    DateTime LinkExpiresAt);
