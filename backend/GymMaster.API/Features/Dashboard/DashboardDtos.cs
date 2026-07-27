namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// Response API 12 chứa toàn bộ số liệu màn Admin Dashboard trong một lần gọi:
/// doanh thu, membership, check-in, giao dịch chờ, tải cơ sở và giờ cao điểm.
/// </summary>
public sealed record DashboardSummaryResponse(
    decimal Revenue,
    int ActiveCount,
    int ExpiredCount,
    IReadOnlyList<CheckInByDayItem> CheckinsByDay,
    decimal PendingPaymentAmount,
    int PendingPaymentCount,
    IReadOnlyList<RevenueByMonthItem> RevenueByMonth,
    IReadOnlyList<ExpiredMembershipItem> RecentlyExpired,
    int FacilityLoadPercent,
    int PtSessionPercent,
    int GeneralAreaPercent,
    decimal PreviousMonthRevenue,
    int NewMembershipsThisMonth,
    int PeakHourStart,
    int PeakHourEnd);

/// <summary>Số lượt check-in của một ngày để frontend hiển thị thống kê.</summary>
public sealed record CheckInByDayItem(
    DateOnly Date,
    int Count);

/// <summary>Doanh thu một tháng trong biểu đồ 6 tháng gần nhất.</summary>
public sealed record RevenueByMonthItem(
    string Month,
    decimal Revenue);

/// <summary>Hội viên vừa hết hạn được hiển thị trong danh sách cảnh báo Dashboard.</summary>
public sealed record ExpiredMembershipItem(
    string Initials,
    string MemberName,
    string PackageName,
    DateOnly ExpiredDate);

/// <summary>
/// Một dòng của API 13 Audit Log; UserDisplayName được ghép từ bảng users
/// và có thể null nếu user không tồn tại hoặc đã soft-delete.
/// </summary>
public sealed record AuditLogResponse(
    long Id,
    long? UserId,
    string? UserDisplayName,
    string Action,
    string EntityType,
    long EntityId,
    string? Metadata,
    DateTime CreatedAt);
