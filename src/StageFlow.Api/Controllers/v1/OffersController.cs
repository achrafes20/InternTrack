using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.InternshipOffers.DTOs;
using StageFlow.Application.Features.InternshipOffers.Services;

namespace StageFlow.Api.Controllers.v1;

public class OffersController : ApiControllerBase
{
    private readonly IInternshipOfferService _offerService;
    private readonly IAccessControlService _accessControl;

    public OffersController(IInternshipOfferService offerService, IAccessControlService accessControl)
    {
        _offerService = offerService;
        _accessControl = accessControl;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetOffers(
        [FromQuery] string? searchTerm,
        [FromQuery] string? company,
        [FromQuery] string? city,
        [FromQuery] string? skill,
        [FromQuery] Guid? trackId,
        [FromQuery] bool includeClosed = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Les offres clôturées (désactivées ou expirées) ne sont visibles que des gestionnaires.
        var canSeeClosed = includeClosed && (_accessControl.IsAdmin || _accessControl.IsDepartmentHead);

        var result = await _offerService.GetOffersAsync(
            searchTerm, company, city, skill, trackId, canSeeClosed, pageNumber, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOfferById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _offerService.GetOfferByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Non trouvé", Detail = result.Error, Status = 404 });

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "RequireDepartmentHeadRole")]
    public async Task<IActionResult> CreateOffer([FromBody] CreateInternshipOfferCommand command, CancellationToken cancellationToken)
    {
        // Un responsable ne publie des offres que pour sa propre filière.
        if (_accessControl.IsDepartmentHead && _accessControl.AcademicTrackId != command.AcademicTrackId)
            return Forbid();

        var result = await _offerService.CreateOfferAsync(command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Erreur de création", Detail = result.Error, Status = 400 });

        return CreatedAtAction(nameof(GetOfferById), new { id = result.Value!.Id }, result.Value);
    }
}
