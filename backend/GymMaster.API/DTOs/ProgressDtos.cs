namespace GymMaster.API.DTOs;

public sealed record RecordProgressRequest(
    DateTime? MeasuredAt,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipCm,
    string? Note);

public sealed record ProgressResponse(
    long Id,
    long MemberId,
    DateTime MeasuredAt,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipCm,
    string? Note,
    DateTime CreatedAt);

public sealed record Profile360Response(
    MemberInfo Member,
    object? CurrentMembership,
    IReadOnlyList<object> MembershipHistory,
    IReadOnlyList<ProgressResponse> ProgressTimeline,
    object? CheckInsSummary,
    object? PtAssignment,
    object? NutritionSummary);

public sealed record MemberInfo(
    long Id,
    long UserId,
    string FullName,
    string Email,
    string? Phone,
    DateTime? DateOfBirth,
    string? Gender);
