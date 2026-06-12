namespace GymMaster.API.DTOs;

public sealed record RecordProgressRequest(
    DateTime? MeasuredAt,
    decimal? WeightKg,
    decimal? BodyFatPct,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipCm,
    string? Note);

public sealed record ProgressResponse(
    long Id,
    long MemberId,
    DateTime MeasuredAt,
    decimal? WeightKg,
    decimal? BodyFatPct,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipCm,
    string? Note,
    DateTime CreatedAt);

// FR-360-01: tong hop day du theo spec 006 — ho so, membership hien tai + lich su,
// check-in gan day, tien do, tom tat dinh duong. AssignedPT cho spec 005 (chua co bang).
public sealed record Profile360Response(
    Member360Info Member,
    Membership360? CurrentMembership,
    IReadOnlyList<Membership360> MembershipHistory,
    IReadOnlyList<CheckIn360> RecentCheckIns,
    IReadOnlyList<ProgressResponse> ProgressTimeline,
    CalorieSummaryResponse? NutritionSummary,
    object? AssignedPT);

public sealed record Member360Info(
    long Id,
    string FullName,
    string Email,
    string? Phone,
    string Status,
    DateTime? DateOfBirth,
    string? Gender);

public sealed record Membership360(
    long Id,
    string PackageName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string PaymentStatus);

public sealed record CheckIn360(
    long Id,
    DateTime CheckInAt);
