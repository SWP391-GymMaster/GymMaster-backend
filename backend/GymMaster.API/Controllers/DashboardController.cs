using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // FR-DASH-01/02 — GET /api/dashboard/summary?from=&to=
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetSummaryAsync(from, to, cancellationToken);
        return ToActionResult(result);
    }
}
