using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Conventions.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Conventions.Services;

public interface IConventionService
{
    Task<Result<byte[]>> DownloadConventionPdfAsync(Guid internshipId, CancellationToken cancellationToken);
    Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken);
    Task<Result> SignAsync(Guid internshipId, ConventionParty party, CancellationToken cancellationToken);
}

public class ConventionService : IConventionService
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IQrCodeService _qrCodeService;
    private readonly IConfiguration _configuration;
    private readonly IIdentityService _identityService;

    public ConventionService(
        IApplicationDbContext context,
        IPdfGenerator pdfGenerator,
        IQrCodeService qrCodeService,
        IConfiguration configuration,
        IIdentityService identityService)
    {
        _context = context;
        _pdfGenerator = pdfGenerator;
        _qrCodeService = qrCodeService;
        _configuration = configuration;
        _identityService = identityService;
    }

    public async Task<Result<byte[]>> DownloadConventionPdfAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        // Lecture seule : la convention est créée lors de la validation du dossier, jamais lors d'un GET.
        var internship = await _context.Internships
            .AsNoTracking()
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
            .FirstOrDefaultAsync(i => i.Id == internshipId, cancellationToken);

        if (internship == null)
            return Result<byte[]>.Failure("Stage introuvable.");

        var convention = internship.Convention;
        if (convention == null)
            return Result<byte[]>.Failure("Aucune convention n'a encore été générée : le dossier doit d'abord être validé par le responsable de filière.");

        var student = await _identityService.GetUserByIdAsync(internship.StudentId, cancellationToken);
        var studentFullName = student.IsSuccess ? student.Value!.FullName : "Élève Ingénieur ENSA";
        var studentEmail = student.IsSuccess ? student.Value!.Email : "—";
        var studentNumber = student.IsSuccess && !string.IsNullOrWhiteSpace(student.Value!.StudentNumber)
            ? student.Value!.StudentNumber!
            : "—";

        var verificationUrl = BuildVerificationUrl(convention.VerificationToken);
        var qrCodeBytes = _qrCodeService.GenerateQrCodePng(verificationUrl);

        var pdfBytes = _pdfGenerator.GenerateConventionPdf(new ConventionPdfModel(
            convention.ConventionNumber,
            studentFullName,
            studentEmail,
            studentNumber,
            internship.AcademicTrack.Name,
            internship.Company.Name,
            internship.Company.Address,
            internship.CompanySupervisorName,
            internship.Title,
            internship.SubjectDescription,
            internship.Period.StartDate,
            internship.Period.EndDate,
            convention.IsSignedByStudent,
            convention.IsSignedByCompany,
            convention.IsSignedBySchool,
            convention.SignedAt,
            verificationUrl,
            qrCodeBytes));

        return Result<byte[]>.Success(pdfBytes);
    }

    public async Task<Result> SignAsync(Guid internshipId, ConventionParty party, CancellationToken cancellationToken)
    {
        var convention = await _context.Conventions
            .Include(c => c.Internship)
            .FirstOrDefaultAsync(c => c.InternshipId == internshipId, cancellationToken);

        if (convention == null)
            return Result.Failure("Aucune convention n'existe pour ce stage.");

        if (convention.Internship.Status is InternshipStatus.Brouillon or InternshipStatus.Propose or InternshipStatus.Refuse)
            return Result.Failure("La convention ne peut être signée que pour un dossier validé.");

        var result = convention.Sign(party);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationToken) || verificationToken.Length > 64)
            return Result<ConventionVerificationDto>.Failure("Jeton de vérification introuvable. Ce document n'est pas authentique.");

        var convention = await _context.Conventions
            .Include(c => c.Internship)
                .ThenInclude(i => i.Company)
            .Include(c => c.Internship)
                .ThenInclude(i => i.AcademicTrack)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.VerificationToken == verificationToken, cancellationToken);

        if (convention == null)
            return Result<ConventionVerificationDto>.Failure("Jeton de vérification introuvable. Ce document n'est pas authentique.");

        var student = await _identityService.GetUserByIdAsync(convention.Internship.StudentId, cancellationToken);
        var studentFullName = student.IsSuccess ? student.Value!.FullName : "Élève Ingénieur ENSA";

        var dto = new ConventionVerificationDto(
            convention.ConventionNumber,
            studentFullName,
            convention.Internship.AcademicTrack.Name,
            convention.Internship.Company.Name,
            convention.Internship.Period.StartDate,
            convention.Internship.Period.EndDate,
            IsAuthentic: true,
            convention.IsSignedByStudent,
            convention.IsSignedByCompany,
            convention.IsSignedBySchool,
            convention.IsFullySigned,
            convention.SignedAt);

        return Result<ConventionVerificationDto>.Success(dto);
    }

    private string BuildVerificationUrl(string token)
    {
        var frontendBaseUrl = _configuration["AppConfig:FrontendBaseUrl"] ?? "http://localhost:5173";
        return $"{frontendBaseUrl.TrimEnd('/')}/verify-convention/{token}";
    }
}
