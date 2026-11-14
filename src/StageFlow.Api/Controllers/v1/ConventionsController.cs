using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Conventions.Services;

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
