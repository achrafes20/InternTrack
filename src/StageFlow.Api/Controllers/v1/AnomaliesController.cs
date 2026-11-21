using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Security;
using StageFlow.Application.Features.Anomalies;

namespace StageFlow.Api.Controllers.v1;

/// <summary>
/// Rapport d'anomalies : incohérences des données (notamment antérieures aux règles actuelles).
/// L'administrateur voit tout ; un responsable de filière, sa seule filière.
/// </summary>
[Authorize(Policy = "RequireDepartmentHeadRole")]
public class AnomaliesController : ApiControllerBase
{
    private readonly IAnomalyService _anomalyService;
    private readonly IAccessControlService _accessControl;

    public AnomaliesController(IAnomalyService anomalyService, IAccessControlService accessControl)
    {
        _anomalyService = anomalyService;
        _accessControl = accessControl;
    }

    [HttpGet]
    public async Task<IActionResult> GetAnomalies(CancellationToken cancellationToken)
    {
        if (_accessControl.IsDepartmentHead && !_accessControl.AcademicTrackId.HasValue)
            return Ok(Array.Empty<AnomalyDto>());

        var trackId = _accessControl.IsAdmin ? null : _accessControl.AcademicTrackId;
        return Ok(await _anomalyService.DetectAsync(trackId, cancellationToken));
    }
}
