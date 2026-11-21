namespace StageFlow.Application.Common.Interfaces;

public record ConventionPdfModel(
    string ConventionNumber,
    string StudentFullName,
    string StudentEmail,
    string StudentNumber,
    string TrackName,
    string CompanyName,
    string CompanyAddress,
    string SupervisorName,
    string InternshipTitle,
    string SubjectDescription,
    DateTime StartDate,
    DateTime EndDate,
    bool IsSignedByStudent,
    bool IsSignedByCompany,
    bool IsSignedBySchool,
    DateTime? SignedAt,
    string VerificationUrl,
    byte[] QrCodeBytes,
    string? CompanySignerName = null,
    string? SignedAtLabel = null,
    string? GeneratedAtLabel = null);

public interface IPdfGenerator
{
    byte[] GenerateConventionPdf(ConventionPdfModel model);
}
