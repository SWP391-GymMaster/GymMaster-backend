using GymMaster.API.Features.Nutrition;
namespace GymMaster.API.Features.Training;
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
// check-in gan day, tien do, tom tat dinh duong, PT dang phan cong (spec 005 da co bang).
public sealed record Profile360Response(
    Member360Info Member,
    Membership360? CurrentMembership,
    IReadOnlyList<Membership360> MembershipHistory,
    IReadOnlyList<CheckIn360> RecentCheckIns,
    IReadOnlyList<ProgressResponse> ProgressTimeline,
    CalorieSummaryResponse? NutritionSummary,
    AssignedPt360? AssignedPT);

public sealed record Member360Info(
    long Id,
    string MemberCode,
    string FullName,
    string Email,
    string? AvatarUrl,
    string? Phone,
    string Status,
    DateTime? DateOfBirth,
    string? Gender);

// PT dang phan cong active cho member (khop FE: id, fullName, specialty, assignedAt).
public sealed record AssignedPt360(
    long Id,
    string FullName,
    string? Specialty,
    DateTime AssignedAt);

public sealed record Membership360(
    long Id,
    long PackageId,
    string PackageName,
    bool SupportsPT,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string PaymentStatus);

public sealed record CheckIn360(
    long Id,
    DateTime CheckInAt);
