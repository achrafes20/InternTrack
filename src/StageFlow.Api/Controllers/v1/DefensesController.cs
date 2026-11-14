using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;

namespace StageFlow.Api.Controllers.v1;

[Authorize]
public class DefensesController : ApiControllerBase
{
    private readonly IDefenseService _defenseService;
    private readonly IAccessControlService _accessControl;

    public DefensesController(IDefenseService defenseService, IAccessControlService accessControl)
    {
        _defenseService = defenseService;
        _accessControl = accessControl;
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
        var result = await _defenseService.CheckConflictAsync(request, cancellationToken);
        return Ok(result);
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

    [HttpPost("{id:guid}/grade")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> RecordGrade(Guid id, [FromBody] RecordGradeCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageDefenseAsync(id, cancellationToken))
            return Forbid();

        var currentUserId = _accessControl.UserId!;
        var result = await _defenseService.RecordGradeAsync(id, command, currentUserId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Saisie de note impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }
}
