using GymMaster.API.Common;
namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// Hợp đồng hai nghiệp vụ đọc của feature Dashboard:
/// tổng hợp chỉ số vận hành và tra cứu Audit Log.
/// </summary>
public interface IDashboardService
{
    // API 12: tổng hợp số liệu cho Admin Dashboard; không có dữ liệu thì trả số 0/danh sách rỗng.
    Task<ServiceResult<DashboardSummaryResponse>> GetSummaryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);

    // API 13: lọc/phân trang Audit Log, sắp xếp bản ghi mới nhất trước.
    Task<ServiceResult<PagedResult<AuditLogResponse>>> GetAuditLogsAsync(
        long? userId,
        string? action,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
