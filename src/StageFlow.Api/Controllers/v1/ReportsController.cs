using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.ProgressReports.DTOs;
using StageFlow.Application.Features.ProgressReports.Services;

namespace StageFlow.Api.Controllers.v1;

[Authorize]
public class ReportsController : ApiControllerBase
{
    private readonly IProgressReportService _reportService;
    private readonly IAiSummaryService _aiSummaryService;
    private readonly IAccessControlService _accessControl;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public ReportsController(
        IProgressReportService reportService,
        IAiSummaryService aiSummaryService,
        IAccessControlService accessControl,
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _reportService = reportService;
        _aiSummaryService = aiSummaryService;
        _accessControl = accessControl;
        _currentUserService = currentUserService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllReports(CancellationToken cancellationToken)
    {
        var reports = await _reportService.GetAllReportsAsync(
            _accessControl.UserId!,
            _accessControl.Role,
            _accessControl.AcademicTrackId,
            cancellationToken);

        return Ok(reports);
    }

    [HttpGet("internship/{internshipId:guid}")]
    public async Task<IActionResult> GetReportsByInternship(Guid internshipId, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _reportService.GetReportsByInternshipAsync(internshipId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> CreateReportFromRoot([FromBody] CreateReportCommand command, CancellationToken cancellationToken)
    {
        if (!command.InternshipId.HasValue || command.InternshipId.Value == Guid.Empty)
            return BadRequest(new ProblemDetails { Title = "Paramètre manquant", Detail = "L'identifiant du stage (InternshipId) est obligatoire.", Status = 400 });

        return await CreateReport(command.InternshipId.Value, command, cancellationToken);
    }

    [HttpPost("internship/{internshipId:guid}")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> CreateReport(Guid internshipId, [FromBody] CreateReportCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var studentId = _currentUserService.UserId!;
        var result = await _reportService.CreateReportAsync(internshipId, command, studentId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Dépôt de rapport impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }

    /// <summary>Modification d'un rapport par l'élève auteur, tant qu'il n'a pas été validé par l'encadrant.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> UpdateReport(Guid id, [FromBody] UpdateReportCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessReportAsync(id, cancellationToken))
            return Forbid();

        var result = await _reportService.UpdateReportAsync(id, command, _currentUserService.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Modification impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }

    /// <summary>Commentaire de l'encadrant / du responsable, ou réponse de l'élève sur son propre rapport.</summary>
    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanCommentOnReportAsync(id, cancellationToken))
            return Forbid();

        var authorId = _currentUserService.UserId!;
        var authorFullName = _currentUserService.UserFullName ?? _currentUserService.UserEmail ?? "Encadrant";

        var result = await _reportService.AddCommentAsync(id, command.Content, authorId, authorFullName, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Ajout de commentaire impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = "RequireSupervisorRole")]
    public async Task<IActionResult> ValidateReport(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanSuperviseReportAsync(id, cancellationToken))
            return Forbid();

        var supervisorId = _currentUserService.UserId!;
        var result = await _reportService.ValidateReportAsync(id, supervisorId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Validation de rapport impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/summarize")]
    [Authorize(Policy = "RequireSupervisorRole")]
    public async Task<IActionResult> SummarizeReport(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanSuperviseReportAsync(id, cancellationToken))
            return Forbid();

        var report = await _context.ProgressReports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (report == null)
            return NotFound(new ProblemDetails { Title = "Rapport introuvable", Status = 404 });

        var summary = await _aiSummaryService.SummarizeReportAsync(
            report.ActivitiesPerformed,
            report.SkillsAcquired,
            report.DifficultiesEncountered,
            report.NextWeekGoals,
            cancellationToken);

        report.SetAiSummary(summary);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { summary });
    }
}
