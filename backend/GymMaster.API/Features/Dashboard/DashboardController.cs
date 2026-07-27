using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// API tổng quan vận hành chỉ dành cho Admin.
/// Controller nhận khoảng ngày doanh thu và giao toàn bộ truy vấn/tính toán
/// cho DashboardService.
/// </summary>
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

    // 12 GET /api/v1/dashboard/summary?from=&to=
    // Mục đích: lấy toàn bộ số liệu cho Admin Dashboard trong một response:
    // doanh thu, membership, check-in, giao dịch chờ, tải cơ sở và giờ cao điểm.
    // Xử lý thật: DashboardService.GetSummaryAsync;
    // response: DashboardSummaryResponse trong DashboardDtos.cs.
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
