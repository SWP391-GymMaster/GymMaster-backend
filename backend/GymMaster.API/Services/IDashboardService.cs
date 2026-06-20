using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IDashboardService
{
    // FR-DASH-01/02: tong hop chi so trong khoang ngay (0 neu khong co du lieu - FR-DASH-03).
    Task<AuthServiceResult<DashboardSummaryResponse>> GetSummaryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);

    // FR-AUD-02: tra cuu audit log phan trang, giam dan theo thoi gian.
    Task<AuthServiceResult<PagedResult<AuditLogResponse>>> GetAuditLogsAsync(
        long? userId,
        string? action,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
