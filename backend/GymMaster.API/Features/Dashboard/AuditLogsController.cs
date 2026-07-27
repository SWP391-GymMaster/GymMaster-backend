using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// API tra cứu Audit Log chỉ dành cho Admin.
/// Controller nhận bộ lọc/phân trang; DashboardService thực hiện truy vấn,
/// ghép tên người thao tác và tạo response.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AuditLogsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // 13 GET /api/v1/audit-logs?userId=&action=&from=&to=&search=&page=&pageSize=
    // Mục đích: xem, tìm kiếm và phân trang lịch sử thao tác của toàn hệ thống.
    // action khớp chính xác; search tìm trong Action hoặc Entity.
    // Xử lý thật: DashboardService.GetAuditLogsAsync;
    // response: PagedResult<AuditLogResponse>.
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] long? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetAuditLogsAsync(
            userId, action, from, to, search, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }
}
