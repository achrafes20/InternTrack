using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;

namespace StageFlow.Api.Controllers.v1;

[Authorize]
public class DefensesController : ApiControllerBase
{
    private readonly IDefenseService _defenseService;
    private readonly IAccessControlService _accessControl;
    private readonly ITutorEvaluationService _tutorEvaluation;

    public DefensesController(IDefenseService defenseService, IAccessControlService accessControl, ITutorEvaluationService tutorEvaluation)
    {
        _defenseService = defenseService;
        _accessControl = accessControl;
        _tutorEvaluation = tutorEvaluation;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDefenses(CancellationToken cancellationToken)
    {
        var result = await _defenseService.GetAllDefensesAsync(
            _accessControl.UserId!,
            _accessControl.Role,
            _accessControl.AcademicTrackId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDefenseById(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessDefenseAsync(id, cancellationToken))
            return Forbid();

        var result = await _defenseService.GetDefenseByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Non trouvé", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }

    [HttpPost("check-conflict")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> CheckConflict([FromBody] CheckConflictRequest request, CancellationToken cancellationToken)
    {
        if (request.InternshipId.HasValue && !await _accessControl.CanManageInternshipAsync(request.InternshipId.Value, cancellationToken))
            return Forbid();

        var result = await _defenseService.CheckConflictAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Enseignants actifs. Sans filtre : tous (composition d'un jury, qui peut inclure des enseignants d'autres filières).
    /// Avec <paramref name="trackId"/> : seuls ceux habilités à encadrer un stage de cette filière.
    /// </summary>
    [HttpGet("/api/v1/teachers")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> GetTeachers([FromQuery] Guid? trackId, CancellationToken cancellationToken)
        => Ok(await _defenseService.GetTeachersAsync(trackId, cancellationToken));

    /// <summary>Évaluation individuelle (note proposée + appréciation) par un membre interne du jury.</summary>
    [HttpPut("{id:guid}/evaluations/me")]
    [Authorize(Policy = "RequireSupervisorRole")]
    public async Task<IActionResult> SubmitEvaluation(Guid id, [FromBody] SubmitEvaluationCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessDefenseAsync(id, cancellationToken))
            return Forbid();

        var result = await _defenseService.SubmitEvaluationAsync(id, command, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Évaluation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Correction d'une note déjà enregistrée : responsable de filière ou administrateur, motif obligatoire.</summary>
    [HttpPut("{id:guid}/grade")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> CorrectGrade(Guid id, [FromBody] CorrectGradeCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageDefenseAsync(id, cancellationToken))
            return Forbid();

        var result = await _defenseService.CorrectGradeAsync(id, command, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Correction impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> ScheduleDefense([FromBody] ScheduleDefenseCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(command.InternshipId, cancellationToken))
            return Forbid();

        var currentUserId = _accessControl.UserId!;
        var result = await _defenseService.ScheduleDefenseAsync(command, currentUserId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Conflit ou planification impossible", Detail = result.Error, Status = 400 });

        return CreatedAtAction(nameof(GetDefenseById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Note finale : arrêtée par le président du jury, ou par le responsable de filière / l'administrateur.
    /// </summary>
    [HttpPost("{id:guid}/grade")]
    [Authorize(Policy = "RequireSupervisorRole")]
    public async Task<IActionResult> RecordGrade(Guid id, [FromBody] RecordGradeCommand command, CancellationToken cancellationToken)
    {
        var allowed = await _accessControl.CanManageDefenseAsync(id, cancellationToken)
                      || await _defenseService.IsJuryPresidentAsync(id, _accessControl.UserId!, cancellationToken);
        if (!allowed)
            return Forbid();

        var currentUserId = _accessControl.UserId!;
        var result = await _defenseService.RecordGradeAsync(id, command, currentUserId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Saisie de note impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Envoie au tuteur entreprise (membre externe du jury) son lien personnel d'évaluation.</summary>
    [HttpPost("{id:guid}/tutor-evaluation-link")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> SendTutorEvaluationLink(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageDefenseAsync(id, cancellationToken))
            return Forbid();

        var result = await _tutorEvaluation.SendLinkAsync(id, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Envoi impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpGet("tutor-evaluation/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> GetTutorEvaluation(string token, CancellationToken cancellationToken)
    {
        var result = await _tutorEvaluation.GetInfoAsync(token, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Lien invalide", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }

    [HttpPut("tutor-evaluation/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> SubmitTutorEvaluation(string token, [FromBody] SubmitEvaluationCommand command, CancellationToken cancellationToken)
    {
        var result = await _tutorEvaluation.SubmitAsync(token, command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Évaluation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }
}
