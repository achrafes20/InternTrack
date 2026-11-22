using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using InternTrack.Application.Common.Security;
using InternTrack.Application.Features.Conventions.DTOs;
using InternTrack.Application.Features.Conventions.Services;
using InternTrack.Domain.Entities;

namespace InternTrack.Api.Controllers.v1;

[Authorize]
public class ConventionsController : ApiControllerBase
{
    private readonly IConventionService _conventionService;
    private readonly IAccessControlService _accessControl;
    private readonly IConventionAmendmentService _amendments;

    public ConventionsController(IConventionService conventionService, IAccessControlService accessControl, IConventionAmendmentService amendments)
    {
        _conventionService = conventionService;
        _accessControl = accessControl;
        _amendments = amendments;
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

    /// <summary>Avenants de la convention (historique complet, du plus récent au plus ancien).</summary>
    [HttpGet("internship/{internshipId:guid}/amendments")]
    public async Task<IActionResult> GetAmendments(Guid internshipId, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _amendments.GetForInternshipAsync(internshipId, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Création d'un avenant par l'école (dates, tuteur entreprise, sujet) : l'école le signe à la création.</summary>
    [HttpPost("internship/{internshipId:guid}/amendments")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> CreateAmendment(Guid internshipId, [FromBody] CreateConventionAmendmentCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _amendments.CreateAsync(internshipId, command, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Avenant impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }

    /// <summary>
    /// Signature d'un avenant : l'élève signe depuis son compte ; la signature de l'entreprise (reçue sur papier
    /// ou par e-mail) est enregistrée par l'école avec le nom du signataire. Appliqué au dossier à la 3e signature.
    /// </summary>
    [HttpPost("internship/{internshipId:guid}/amendments/{amendmentId:guid}/sign")]
    public async Task<IActionResult> SignAmendment(Guid internshipId, Guid amendmentId, [FromBody] SignConventionAmendmentCommand command, CancellationToken cancellationToken)
    {
        var allowed = command.Party switch
        {
            ConventionParty.Student => _accessControl.IsStudent && await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken),
            ConventionParty.Company or ConventionParty.School => await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken),
            _ => false
        };
        if (!allowed)
            return Forbid();

        var result = await _amendments.SignAsync(internshipId, amendmentId, command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Signature impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }

    [HttpPost("internship/{internshipId:guid}/amendments/{amendmentId:guid}/withdraw")]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> WithdrawAmendment(Guid internshipId, Guid amendmentId, [FromBody] WithdrawConventionAmendmentCommand command, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        var result = await _amendments.WithdrawAsync(internshipId, amendmentId, command.Reason, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Retrait impossible", Detail = result.Error, Status = 400 });

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
