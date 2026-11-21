using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Internships.DTOs;
using StageFlow.Application.Features.Internships.Services;
using StageFlow.Domain.Enums;

namespace StageFlow.Api.Controllers.v1;

public record AssignSupervisorRequest(string SupervisorId);

[Authorize]
public class InternshipsController : ApiControllerBase
{
    private readonly IInternshipService _internshipService;
    private readonly IAccessControlService _accessControl;
    private readonly IIdentityService _identityService;

    public InternshipsController(
        IInternshipService internshipService,
        IAccessControlService accessControl,
        IIdentityService identityService)
    {
        _internshipService = internshipService;
        _accessControl = accessControl;
        _identityService = identityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInternships(
        [FromQuery] string? studentId,
        [FromQuery] InternshipStatus? status,
        [FromQuery] Guid? trackId,
        [FromQuery] string? academicYear,
        CancellationToken cancellationToken)
    {
        // Chaque rôle ne voit que le périmètre qui lui est autorisé.
        string? supervisorId = null;
        Guid? effectiveTrackId = trackId;

        if (_accessControl.IsStudent)
        {
            studentId = _accessControl.UserId;
        }
        else if (_accessControl.IsSupervisor)
        {
            supervisorId = _accessControl.UserId;
        }
        else if (_accessControl.IsDepartmentHead)
        {
            if (!_accessControl.AcademicTrackId.HasValue)
                return Ok(Array.Empty<InternshipDto>());

            effectiveTrackId = _accessControl.AcademicTrackId;
        }
        else if (!_accessControl.IsAdmin)
        {
            return Forbid();
        }

        var result = await _internshipService.GetAllAsync(studentId, supervisorId, status, effectiveTrackId, cancellationToken, academicYear);
        return Ok(result);
    }

    /// <summary>Années universitaires présentes (filtre des listes et du tableau de bord).</summary>
    [HttpGet("academic-years")]
    public async Task<IActionResult> GetAcademicYears(CancellationToken cancellationToken)
    {
        var trackId = _accessControl.IsAdmin ? null : _accessControl.AcademicTrackId;
        return Ok(await _internshipService.GetAcademicYearsAsync(trackId, cancellationToken));
    }

    /// <summary>Annulation administrative d'un dossier (abandon, doublon, données incohérentes). Motif obligatoire.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> CancelInternship(Guid id, [FromBody] CancelInternshipCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.CancelAsync(id, _accessControl.UserId!, command.Reason, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Annulation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInternshipById(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Non trouvé", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> ProposeInternship([FromBody] ProposeInternshipCommand command, CancellationToken cancellationToken)
    {
        // Un étudiant doit être rattaché à une filière et ne peut proposer un stage que dans celle-ci.
        if (!_accessControl.AcademicTrackId.HasValue)
            return BadRequest(new ProblemDetails { Title = "Compte incomplet", Detail = "Votre compte n'est rattaché à aucune filière. Contactez l'administration.", Status = 400 });

        if (command.AcademicTrackId != _accessControl.AcademicTrackId.Value)
            return Forbid();

        var studentId = _accessControl.UserId!;
        var result = await _internshipService.ProposeAsync(command, studentId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Proposition invalide", Detail = result.Error, Status = 400 });

        return CreatedAtAction(nameof(GetInternshipById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateInternshipDraftCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.UpdateDraftAsync(id, command, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Modification impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> SubmitInternship(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.SubmitAsync(id, _accessControl.UserId!, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Soumission impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "RequireStudentRole")]
    public async Task<IActionResult> ReopenInternship(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.ReopenAsync(id, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Réouverture impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> ApproveInternship(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.ApproveAsync(id, _accessControl.UserId!, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Validation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> RejectInternship(Guid id, [FromBody] RejectInternshipCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.RejectAsync(id, _accessControl.UserId!, command.RejectionReason, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Refus impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> StartInternship(Guid id, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(id, cancellationToken))
            return Forbid();

        var result = await _internshipService.StartInternshipAsync(id, _accessControl.UserId!, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Démarrage impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpPost("{id:guid}/assign-supervisor")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> AssignSupervisor(Guid id, [FromBody] AssignSupervisorRequest request, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(id, cancellationToken))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.SupervisorId))
            return BadRequest(new ProblemDetails { Title = "Paramètre manquant", Detail = "L'identifiant de l'encadrant est obligatoire.", Status = 400 });

        var isSupervisor = await _identityService.IsUserInRoleAsync(request.SupervisorId, UserRole.Encadrant, cancellationToken)
            || await _identityService.IsUserInRoleAsync(request.SupervisorId, UserRole.ResponsableFiliere, cancellationToken);

        if (!isSupervisor)
            return BadRequest(new ProblemDetails { Title = "Encadrant invalide", Detail = "L'utilisateur ciblé doit avoir le rôle Encadrant ou Responsable de filière.", Status = 400 });

        var result = await _internshipService.AssignSupervisorAsync(id, request.SupervisorId, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Affectation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }
}
