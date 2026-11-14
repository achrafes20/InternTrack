using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Conventions.DTOs;
using StageFlow.Domain.Common;

namespace StageFlow.Application.Features.Conventions.Services;

public interface IConventionService
{
    Task<Result<byte[]>> DownloadConventionPdfAsync(Guid internshipId, CancellationToken cancellationToken);
    Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken);
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
        var internship = await _context.Internships
            .Include(i => i.Company)
            .Include(i => i.AcademicTrack)
            .Include(i => i.Convention)
            .FirstOrDefaultAsync(i => i.Id == internshipId, cancellationToken);

        if (internship == null)
            return Result<byte[]>.Failure("Stage introuvable.");

        var convention = internship.Convention;
        if (convention == null)
        {
            if (internship.Status < Domain.Enums.InternshipStatus.Valide)
                return Result<byte[]>.Failure("Convention non disponible pour ce stage (le stage doit être validé ou en cours).");

            var convNumber = $"CONV-ENSA-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            convention = new Domain.Entities.Convention(internship.Id, convNumber);
            convention.SignByStudent();
            convention.SignByCompany();
            convention.SignBySchool();
            await _context.Conventions.AddAsync(convention, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Informations réelles de l'élève ingénieur propriétaire du stage.
        var student = await _identityService.GetUserByIdAsync(internship.StudentId, cancellationToken);
        var studentFullName = student.IsSuccess
            ? student.Value!.FullName
            : "Élève Ingénieur ENSA";
        var studentEmail = student.IsSuccess
            ? student.Value!.Email
            : internship.CompanySupervisorEmail;
        var studentNumber = student.IsSuccess && !string.IsNullOrWhiteSpace(student.Value!.StudentNumber)
            ? student.Value!.StudentNumber!
            : internship.StudentId[..Math.Min(8, internship.StudentId.Length)];

        // URL publique de vérification, configurable par environnement.
        var frontendBaseUrl = _configuration["AppConfig:FrontendBaseUrl"] ?? "http://localhost:5173";
        var verificationUrl = $"{frontendBaseUrl.TrimEnd('/')}/verify-convention/{convention.VerificationToken}";

        var qrCodeBytes = _qrCodeService.GenerateQrCodePng(verificationUrl);

        var pdfBytes = _pdfGenerator.GenerateConventionPdf(
            conventionNumber: convention.ConventionNumber,
            studentFullName: studentFullName,
            studentEmail: studentEmail,
            studentNumber: studentNumber,
            trackName: internship.AcademicTrack.Name,
            companyName: internship.Company.Name,
            companyAddress: internship.Company.Address,
            supervisorName: internship.CompanySupervisorName,
            internshipTitle: internship.Title,
            subjectDescription: internship.SubjectDescription,
            startDate: internship.Period.StartDate,
            endDate: internship.Period.EndDate,
            qrCodeBytes: qrCodeBytes);

        return Result<byte[]>.Success(pdfBytes);
    }

    public async Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken)
    {
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
        var studentFullName = student.IsSuccess
            ? student.Value!.FullName
            : "Élève Ingénieur ENSA";

        var dto = new ConventionVerificationDto(
            convention.ConventionNumber,
            studentFullName,
            convention.Internship.AcademicTrack.Name,
            convention.Internship.Company.Name,
            convention.Internship.Period.StartDate,
            convention.Internship.Period.EndDate,
            IsAuthentic: true,
            IsFullySigned: convention.SignedAt.HasValue,
            convention.SignedAt);

        return Result<ConventionVerificationDto>.Success(dto);
    }
}
