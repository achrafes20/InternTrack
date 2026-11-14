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
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditLedgerService.GetAuditLogsAsync(entityName, action, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}

