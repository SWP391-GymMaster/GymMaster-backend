namespace GymMaster.API.DTOs;

// spec 008 — Dashboard & Audit Log

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

public sealed record CheckInByDayItem(
    DateOnly Date,
    int Count);

public sealed record RevenueByMonthItem(
    string Month,
    decimal Revenue);

public sealed record ExpiredMembershipItem(
    string Initials,
    string MemberName,
    string PackageName,
    DateOnly ExpiredDate);

public sealed record AuditLogResponse(
    long Id,
    long? UserId,
    string? UserDisplayName,
    string Action,
    string EntityType,
    long EntityId,
    string? Metadata,
    DateTime CreatedAt);
