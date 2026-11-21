using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    /// Signature de l'élève (propriétaire du stage) ou de l'école (responsable de la filière / administrateur).
    /// L'entreprise signe via son lien, ou l'école l'enregistre sur dépôt du scan signé.
    /// </summary>
    [HttpPost("internship/{internshipId:guid}/sign")]
    public async Task<IActionResult> Sign(Guid internshipId, [FromBody] SignConventionCommand command, CancellationToken cancellationToken)
    {
        var allowed = command.Party switch
        {
            ConventionParty.Student => _accessControl.IsStudent && await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken),
            ConventionParty.School => await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken),
            ConventionParty.Company => true, // refusé par le service avec un message explicatif
            _ => false
        };

        if (!allowed)
            return Forbid();

        var result = await _conventionService.SignAsync(internshipId, command.Party, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Signature impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Envoie au tuteur entreprise un lien de signature à usage unique (renvoi possible : l'ancien lien est invalidé).</summary>
    [HttpPost("internship/{internshipId:guid}/company-signing-link")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> SendCompanySigningLink(Guid internshipId, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _conventionService.SendCompanySigningLinkAsync(internshipId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Envoi impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Enregistre la signature de l'entreprise reçue sur papier (le scan signé doit avoir été déposé).</summary>
    [HttpPost("internship/{internshipId:guid}/company-signature")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> RecordCompanySignature(Guid internshipId, [FromBody] RecordCompanySignatureCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _conventionService.RecordCompanySignatureAsync(internshipId, command.SignerName, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Signature impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Page publique de signature : informations de la convention associée au lien.</summary>
    [HttpGet("company-signing/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> GetCompanySigningInfo(string token, CancellationToken cancellationToken)
    {
        var result = await _conventionService.GetCompanySigningInfoAsync(token, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Lien invalide", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }

    [HttpPost("company-signing/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> SignByCompany(string token, [FromBody] CompanySignCommand command, CancellationToken cancellationToken)
    {
        var result = await _conventionService.SignByCompanyAsync(token, command, cancellationToken);
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
