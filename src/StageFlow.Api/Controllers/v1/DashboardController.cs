using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Dashboard.Services;

namespace StageFlow.Api.Controllers.v1;

[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IAccessControlService _accessControl;

    public DashboardController(IDashboardService dashboardService, IAccessControlService accessControl)
    {
        _dashboardService = dashboardService;
        _accessControl = accessControl;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetDashboardStatsAsync(
            _accessControl.UserId!,
            _accessControl.Role,
            _accessControl.AcademicTrackId,
            cancellationToken);

        return Ok(stats);
    }
}
