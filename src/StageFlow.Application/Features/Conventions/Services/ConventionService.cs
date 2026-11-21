using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Conventions.DTOs;
using StageFlow.Application.Features.Internships;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Conventions.Services;

public interface IConventionService
{
    Task<Result<byte[]>> DownloadConventionPdfAsync(Guid internshipId, CancellationToken cancellationToken);
    Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken);

    /// <summary>Signature de l'élève ou de l'école. L'entreprise signe via son lien, ou par dépôt du scan.</summary>
    Task<Result> SignAsync(Guid internshipId, ConventionParty party, CancellationToken cancellationToken);

    /// <summary>Envoie (ou renvoie) au tuteur entreprise un lien de signature à usage unique.</summary>
    Task<Result> SendCompanySigningLinkAsync(Guid internshipId, CancellationToken cancellationToken);

    Task<Result<CompanySigningInfoDto>> GetCompanySigningInfoAsync(string token, CancellationToken cancellationToken);
    Task<Result> SignByCompanyAsync(string token, CompanySignCommand command, CancellationToken cancellationToken);

    /// <summary>Signature entreprise reçue sur papier : exige que le scan de la convention signée ait été déposé.</summary>
    Task<Result> RecordCompanySignatureAsync(Guid internshipId, string signerName, CancellationToken cancellationToken);

    /// <summary>
    /// Génère et conserve l'exemplaire de référence d'une convention entièrement signée, avec son empreinte SHA-256.
    /// Idempotent : un exemplaire déjà figé n'est jamais régénéré.
    /// </summary>
    Task<Result> FreezeSignedDocumentAsync(Guid conventionId, CancellationToken cancellationToken);
}

public class ConventionService : IConventionService
{
    private static readonly InternshipStatus[] SignableStatuses =
    {
        InternshipStatus.Valide, InternshipStatus.EnCours, InternshipStatus.SoutenancePlanifiee, InternshipStatus.Termine
    };

    private readonly IApplicationDbContext _context;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IQrCodeService _qrCodeService;
    private readonly IConfiguration _configuration;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;
    private readonly ITimeZoneService _timeZone;
    private readonly InternshipPolicy _policy;
    private readonly IFileStorage _storage;

    public ConventionService(
        IApplicationDbContext context,
        IPdfGenerator pdfGenerator,
        IQrCodeService qrCodeService,
        IConfiguration configuration,
        IIdentityService identityService,
        IEmailSender emailSender,
        ITimeZoneService timeZone,
        InternshipPolicy policy,
        IFileStorage storage)
    {
        _storage = storage;
        _context = context;
        _pdfGenerator = pdfGenerator;
        _qrCodeService = qrCodeService;
        _configuration = configuration;
        _identityService = identityService;
        _emailSender = emailSender;
        _timeZone = timeZone;
        _policy = policy;
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

        // Convention signée : on sert toujours l'exemplaire figé, dont l'empreinte est publiée sur la page de vérification.
        if (convention.IsFullySigned)
        {
            if (convention.SignedDocumentStorageKey == null)
            {
                var frozen = await FreezeSignedDocumentAsync(convention.Id, cancellationToken);
                if (frozen.IsFailure) return Result<byte[]>.Failure(frozen.Error);
                convention = await _context.Conventions.AsNoTracking().FirstAsync(c => c.Id == convention.Id, cancellationToken);
            }

            await using var stored = await _storage.OpenReadAsync(convention.SignedDocumentStorageKey!, cancellationToken);
            if (stored != null)
            {
                using var copy = new MemoryStream();
                await stored.CopyToAsync(copy, cancellationToken);
                return Result<byte[]>.Success(copy.ToArray());
            }
            // Fichier perdu côté stockage : on régénère une copie (son empreinte ne correspondra plus).
        }

        return Result<byte[]>.Success(await BuildPdfAsync(internship, convention, cancellationToken));
    }

    public async Task<Result> FreezeSignedDocumentAsync(Guid conventionId, CancellationToken cancellationToken)
    {
        var convention = await _context.Conventions
            .Include(c => c.Internship).ThenInclude(i => i.Company)
            .Include(c => c.Internship).ThenInclude(i => i.AcademicTrack)
            .FirstOrDefaultAsync(c => c.Id == conventionId, cancellationToken);

        if (convention == null)
            return Result.Failure("Convention introuvable.");
        if (convention.SignedDocumentStorageKey != null)
            return Result.Success();
        if (!convention.IsFullySigned)
            return Result.Failure("La convention n'est pas encore signée par les trois parties.");

        var pdf = await BuildPdfAsync(convention.Internship, convention, cancellationToken);
        using var content = new MemoryStream(pdf);
        var key = await _storage.SaveAsync(content, "pdf", cancellationToken);

        var result = convention.FreezeSignedDocument(key, SecureToken.Fingerprint(pdf));
        if (result.IsFailure)
        {
            await _storage.DeleteAsync(key, CancellationToken.None);
            return result;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<byte[]> BuildPdfAsync(Internship internship, Convention convention, CancellationToken cancellationToken)
    {

        var student = await _identityService.GetUserByIdAsync(internship.StudentId, cancellationToken);
        var studentFullName = student.IsSuccess ? student.Value!.FullName : "Élève Ingénieur ENSA";
        var studentEmail = student.IsSuccess ? student.Value!.Email : "—";
        var studentNumber = student.IsSuccess && !string.IsNullOrWhiteSpace(student.Value!.StudentNumber)
            ? student.Value!.StudentNumber!
            : "—";

        var verificationUrl = BuildFrontendUrl($"/verify-convention/{convention.VerificationToken}");
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
            qrCodeBytes,
            convention.CompanySignerName,
            convention.SignedAt.HasValue ? _timeZone.FormatDateTime(convention.SignedAt.Value) : null,
            _timeZone.FormatDateTime(DateTime.UtcNow)));

        return pdfBytes;
    }

    public async Task<Result> SignAsync(Guid internshipId, ConventionParty party, CancellationToken cancellationToken)
    {
        if (party == ConventionParty.Company)
            return Result.Failure("L'entreprise signe via le lien qui lui est envoyé, ou par dépôt du scan de la convention signée.");

        var convention = await LoadSignableConventionAsync(internshipId, cancellationToken);
        if (convention.IsFailure)
            return convention;

        var result = convention.Value!.Sign(party);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SendCompanySigningLinkAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        var loaded = await LoadSignableConventionAsync(internshipId, cancellationToken);
        if (loaded.IsFailure)
            return loaded;

        var convention = loaded.Value!;
        var issued = convention.IssueCompanySigningToken(DateTime.UtcNow, TimeSpan.FromDays(_policy.CompanySigningLinkValidityDays));
        if (issued.IsFailure)
            return issued;

        await _context.SaveChangesAsync(cancellationToken);

        var internship = convention.Internship;
        var link = BuildFrontendUrl($"/convention/sign/{issued.Value}");
        await _emailSender.SendEmailAsync(
            internship.CompanySupervisorEmail,
            $"[StageFlow ENSA] Convention de stage à signer : {internship.Title}",
            $@"<p>Bonjour {WebUtility.HtmlEncode(internship.CompanySupervisorName)},</p>
               <p>La convention de stage <strong>{WebUtility.HtmlEncode(convention.ConventionNumber)}</strong>
                  pour le stage « {WebUtility.HtmlEncode(internship.Title)} » attend la signature de votre entreprise.</p>
               <p><a href=""{link}"">Consulter et signer la convention</a></p>
               <p>Ce lien personnel est valable jusqu'au {_timeZone.FormatDateTime(convention.CompanySigningTokenExpiresAt!.Value)} et ne peut servir qu'une fois.</p>",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CompanySigningInfoDto>> GetCompanySigningInfoAsync(string token, CancellationToken cancellationToken)
    {
        var convention = await FindByCompanyTokenAsync(token, cancellationToken);
        if (convention == null || !convention.IsCompanySigningTokenValid(token, DateTime.UtcNow))
            return Result<CompanySigningInfoDto>.Failure("Ce lien de signature est invalide, a expiré ou a déjà été utilisé.");

        var internship = convention.Internship;
        var student = await _identityService.GetUserByIdAsync(internship.StudentId, cancellationToken);

        return Result<CompanySigningInfoDto>.Success(new CompanySigningInfoDto(
            convention.ConventionNumber,
            internship.Title,
            internship.SubjectDescription,
            student.IsSuccess ? student.Value!.FullName : "Élève Ingénieur ENSA",
            internship.AcademicTrack.Name,
            internship.Company.Name,
            internship.CompanySupervisorName,
            internship.Period.StartDate,
            internship.Period.EndDate,
            convention.CompanySigningTokenExpiresAt!.Value));
    }

    public async Task<Result> SignByCompanyAsync(string token, CompanySignCommand command, CancellationToken cancellationToken)
    {
        if (!command.AcceptTerms)
            return Result.Failure("Vous devez accepter les termes de la convention pour la signer.");

        var convention = await FindByCompanyTokenAsync(token, cancellationToken);
        if (convention == null)
            return Result.Failure("Ce lien de signature est invalide, a expiré ou a déjà été utilisé.");

        var result = convention.SignByCompanyWithToken(token, command.SignerName, DateTime.UtcNow);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RecordCompanySignatureAsync(Guid internshipId, string signerName, CancellationToken cancellationToken)
    {
        var loaded = await LoadSignableConventionAsync(internshipId, cancellationToken);
        if (loaded.IsFailure)
            return loaded;

        var hasSignedScan = await _context.InternshipDocuments.AsNoTracking()
            .AnyAsync(d => d.InternshipId == internshipId && d.Kind == InternshipDocumentKind.SignedConvention, cancellationToken);
        if (!hasSignedScan)
            return Result.Failure("Déposez d'abord le scan de la convention signée par l'entreprise : il sert de preuve de la signature.");

        var result = loaded.Value!.RecordCompanySignature(signerName, DateTime.UtcNow);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ConventionVerificationDto>> VerifyConventionAsync(string verificationToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationToken) || verificationToken.Length > FieldLimits.VerificationToken)
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
            convention.SignedAt,
            convention.SignedDocumentSha256);

        return Result<ConventionVerificationDto>.Success(dto);
    }

    private async Task<Result<Convention>> LoadSignableConventionAsync(Guid internshipId, CancellationToken cancellationToken)
    {
        var convention = await _context.Conventions
            .Include(c => c.Internship)
            .FirstOrDefaultAsync(c => c.InternshipId == internshipId, cancellationToken);

        if (convention == null)
            return Result<Convention>.Failure("Aucune convention n'existe pour ce stage.");

        if (!SignableStatuses.Contains(convention.Internship.Status))
            return Result<Convention>.Failure("La convention ne peut être signée que pour un dossier validé.");

        return Result<Convention>.Success(convention);
    }

    private Task<Convention?> FindByCompanyTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return Task.FromResult<Convention?>(null);

        var hash = Convention.HashSigningToken(token);
        return _context.Conventions
            .Include(c => c.Internship).ThenInclude(i => i.Company)
            .Include(c => c.Internship).ThenInclude(i => i.AcademicTrack)
            .FirstOrDefaultAsync(c => c.CompanySigningTokenHash == hash, cancellationToken);
    }

    private string BuildFrontendUrl(string path)
    {
        var frontendBaseUrl = _configuration["AppConfig:FrontendBaseUrl"] ?? "http://localhost:5173";
        return $"{frontendBaseUrl.TrimEnd('/')}{path}";
    }
}
