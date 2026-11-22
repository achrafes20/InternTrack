using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternTrack.Application.Common.Models;
using InternTrack.Application.Common.Security;
using InternTrack.Application.Features.Dashboard.Services;

namespace InternTrack.Api.Controllers.v1;

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
    public async Task<IActionResult> GetDashboardStats([FromQuery] string? academicYear, CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetDashboardStatsAsync(
            _accessControl.UserId!,
            _accessControl.Role,
            _accessControl.AcademicTrackId,
            cancellationToken,
            academicYear);

        return Ok(stats);
    }

    /// <summary>Export CSV (Excel) des stages du périmètre de l'utilisateur, éventuellement filtré par année universitaire.</summary>
    [HttpGet("export")]
    [Authorize(Policy = "RequireSupervisorRole")]
    public async Task<IActionResult> Export([FromQuery] string? academicYear, CancellationToken cancellationToken)
    {
        var csv = await _dashboardService.ExportCsvAsync(
            _accessControl.UserId!,
            _accessControl.Role,
            _accessControl.AcademicTrackId,
            academicYear,
            cancellationToken);

        var suffix = string.IsNullOrWhiteSpace(academicYear) ? "toutes-annees" : academicYear;
        return File(CsvBuilder.ToExcelBytes(csv), "text/csv; charset=utf-8", $"stages-{suffix}.csv");
    }
}
