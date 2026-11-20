using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Features.AuditLedger.Services;

namespace StageFlow.Api.Controllers.v1;

[Authorize(Policy = "RequireAdminRole")]
public class AuditController : ApiControllerBase
{
    private readonly IAuditLedgerService _auditLedgerService;

    public AuditController(IAuditLedgerService auditLedgerService)
    {
        _auditLedgerService = auditLedgerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityName,
        [FromQuery] string? action,
        [FromQuery] string? user,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Les bornes de date sont interprétées en UTC (l'horodatage du journal est en UTC).
        var filter = new AuditLogFilter(
            entityName,
            action,
            user,
            from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : null,
            to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : null);

        var result = await _auditLedgerService.GetAuditLogsAsync(filter, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}
