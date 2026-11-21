using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Application.Features.Defenses.Services;

public record TutorEvaluationInfoDto(
    string StudentFullName,
    string InternshipTitle,
    string CompanyName,
    string TutorName,
    DateTime ScheduledAt,
    string ScheduledAtLabel,
    string Classroom,
    bool HasStarted,
    decimal? CurrentGrade,
    string? CurrentComment,
    DateTime LinkExpiresAt);

/// <summary>
/// Évaluation de la soutenance par le tuteur entreprise, membre externe du jury sans compte :
/// il reçoit un lien personnel, comme pour la signature de la convention.
/// </summary>
public interface ITutorEvaluationService
{
    Task<Result> SendLinkAsync(Guid defenseId, CancellationToken cancellationToken);
    Task<Result<TutorEvaluationInfoDto>> GetInfoAsync(string token, CancellationToken cancellationToken);
    Task<Result> SubmitAsync(string token, SubmitEvaluationCommand command, CancellationToken cancellationToken);
}

public class TutorEvaluationService : ITutorEvaluationService
{
    private const int LinkValidityDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;
    private readonly ITimeZoneService _timeZone;
    private readonly IConfiguration _configuration;

    public TutorEvaluationService(
        IApplicationDbContext context,
        IIdentityService identityService,
        IEmailSender emailSender,
        ITimeZoneService timeZone,
        IConfiguration configuration)
    {
        _context = context;
        _identityService = identityService;
        _emailSender = emailSender;
        _timeZone = timeZone;
        _configuration = configuration;
    }

    public async Task<Result> SendLinkAsync(Guid defenseId, CancellationToken cancellationToken)
    {
        var defense = await _context.Defenses
            .Include(d => d.JuryMembers)
            .Include(d => d.Internship)
            .FirstOrDefaultAsync(d => d.Id == defenseId, cancellationToken);
        if (defense == null)
            return Result.Failure("Soutenance introuvable.");

        var issued = defense.IssueTutorEvaluationToken(DateTime.UtcNow, TimeSpan.FromDays(LinkValidityDays));
        if (issued.IsFailure)
            return issued;

        await _context.SaveChangesAsync(cancellationToken);

        var frontendBaseUrl = (_configuration["AppConfig:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendBaseUrl}/defense/evaluate/{issued.Value}";
        var internship = defense.Internship;

        await _emailSender.SendEmailAsync(
            internship.CompanySupervisorEmail,
            $"[StageFlow ENSA] Évaluation de la soutenance : {internship.Title}",
            $@"<p>Bonjour {WebUtility.HtmlEncode(defense.CompanyTutor!.TeacherFullName)},</p>
               <p>Vous siégez au jury de la soutenance du stage « {WebUtility.HtmlEncode(internship.Title)} »,
                  le {_timeZone.FormatDateTime(defense.ScheduledAt)}, salle {WebUtility.HtmlEncode(defense.Classroom)}.</p>
               <p>Après la soutenance, merci de déposer votre évaluation (note et appréciation) :</p>
               <p><a href=""{link}"">Évaluer la soutenance</a></p>
               <p>Ce lien personnel reste valable jusqu'à l'arrêt de la note finale par le jury (au plus tard le
                  {_timeZone.FormatDateTime(defense.TutorEvaluationTokenExpiresAt!.Value)}).</p>",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<TutorEvaluationInfoDto>> GetInfoAsync(string token, CancellationToken cancellationToken)
    {
        var defense = await FindByTokenAsync(token, cancellationToken);
        if (defense == null || !defense.IsTutorEvaluationTokenValid(token, DateTime.UtcNow))
            return Result<TutorEvaluationInfoDto>.Failure("Ce lien d'évaluation est invalide, a expiré ou n'est plus utilisable (note finale arrêtée).");

        var tutor = defense.CompanyTutor!;
        var mine = defense.Evaluations.FirstOrDefault(e => e.TeacherId == tutor.TeacherId);
        var student = await _identityService.GetUserByIdAsync(defense.Internship.StudentId, cancellationToken);

        return Result<TutorEvaluationInfoDto>.Success(new TutorEvaluationInfoDto(
            student.IsSuccess ? student.Value!.FullName : "Élève ingénieur",
            defense.Internship.Title,
            defense.Internship.Company.Name,
            tutor.TeacherFullName,
            defense.ScheduledAt,
            _timeZone.FormatDateTime(defense.ScheduledAt),
            defense.Classroom,
            DateTime.UtcNow >= defense.ScheduledAt,
            mine?.Grade,
            mine?.Comment,
            defense.TutorEvaluationTokenExpiresAt!.Value));
    }

    public async Task<Result> SubmitAsync(string token, SubmitEvaluationCommand command, CancellationToken cancellationToken)
    {
        var defense = await FindByTokenAsync(token, cancellationToken);
        if (defense == null)
            return Result.Failure("Ce lien d'évaluation est invalide, a expiré ou n'est plus utilisable.");

        var result = defense.SubmitTutorEvaluation(token, command.Grade, command.Comment, DateTime.UtcNow);
        if (result.IsFailure)
            return result;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<Defense?> FindByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return Task.FromResult<Defense?>(null);

        var hash = SecureToken.Hash(token);
        return _context.Defenses
            .Include(d => d.JuryMembers)
            .Include(d => d.Evaluations)
            .Include(d => d.Internship).ThenInclude(i => i.Company)
            .FirstOrDefaultAsync(d => d.TutorEvaluationTokenHash == hash, cancellationToken);
    }
}
