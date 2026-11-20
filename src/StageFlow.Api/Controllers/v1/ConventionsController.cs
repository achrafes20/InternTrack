using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Conventions.DTOs;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Domain.Entities;

namespace StageFlow.Api.Controllers.v1;

[Authorize]
public class ConventionsController : ApiControllerBase
{
    private readonly IConventionService _conventionService;
    private readonly IAccessControlService _accessControl;

    public ConventionsController(IConventionService conventionService, IAccessControlService accessControl)
    {
        _conventionService = conventionService;
        _accessControl = accessControl;
    }

    [HttpGet("internship/{internshipId:guid}/pdf")]
    public async Task<IActionResult> DownloadConventionPdf(Guid internshipId, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _conventionService.DownloadConventionPdfAsync(internshipId, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Convention non disponible", Detail = result.Error, Status = 404 });

        return File(result.Value!, "application/pdf", $"convention_{internshipId}.pdf");
    }

    /// <summary>
    /// Enregistre la signature d'une partie :
    /// - Student : uniquement l'élève propriétaire du stage ;
    /// - School : le responsable de la filière (ou un administrateur) ;
    /// - Company : le responsable de filière (ou un administrateur), à réception de l'exemplaire signé par l'entreprise.
    /// </summary>
    [HttpPost("internship/{internshipId:guid}/sign")]
    public async Task<IActionResult> Sign(Guid internshipId, [FromBody] SignConventionCommand command, CancellationToken cancellationToken)
    {
        var allowed = command.Party switch
        {
            ConventionParty.Student => _accessControl.IsStudent && await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken),
            ConventionParty.School or ConventionParty.Company => await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken),
            _ => false
        };

        if (!allowed)
            return Forbid();

        var result = await _conventionService.SignAsync(internshipId, command.Party, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Signature impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    [HttpGet("verify/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyConvention(string token, CancellationToken cancellationToken)
    {
        var result = await _conventionService.VerifyConventionAsync(token, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Document non certifié", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }
}
