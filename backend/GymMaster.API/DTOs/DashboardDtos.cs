namespace GymMaster.API.DTOs;

// spec 008 — Dashboard & Audit Log

public sealed record DashboardSummaryResponse(
    decimal Revenue,
    int ActiveCount,
    int ExpiredCount,
    IReadOnlyList<CheckInByDayItem> CheckinsByDay);

public sealed record CheckInByDayItem(
    DateOnly Date,
    int Count);

public sealed record AuditLogResponse(
    long Id,
    long? UserId,
    string? UserDisplayName,
    string Action,
    string EntityType,
    long EntityId,
    string? Metadata,
    DateTime CreatedAt);
