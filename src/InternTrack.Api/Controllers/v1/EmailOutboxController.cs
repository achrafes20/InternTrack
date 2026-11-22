using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Api.Controllers.v1;

/// <summary>Supervision de la file d'e-mails (outbox) : réservée à l'administrateur.</summary>
[Authorize(Policy = "RequireAdminRole")]
[Route("api/v1/email-outbox")]
public class EmailOutboxController : ApiControllerBase
{
    private readonly IEmailOutboxMonitor _monitor;

    public EmailOutboxController(IEmailOutboxMonitor monitor)
    {
        _monitor = monitor;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        => Ok(await _monitor.GetSummaryAsync(cancellationToken));

    /// <summary>Derniers e-mails, filtrables par statut (Pending, Sent, Failed).</summary>
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] string? status, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
        => Ok(await _monitor.GetRecentAsync(status, take, cancellationToken));

    [HttpPost("{id:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken)
    {
        var result = await _monitor.RequeueAsync(id, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Remise en file impossible", Detail = result.Error, Status = 400 });
        return NoContent();
    }
}
