namespace GymMaster.API.DTOs;

public sealed record CreatePackageRequest(
    string Name,
    short DurationDays,
    decimal Price,
    string? Description);

public sealed record UpdatePackageRequest(
    string? Name,
    short? DurationDays,
    decimal? Price,
    string? Description,
    bool? IsActive);

public sealed record PackageResponse(
    long Id,
    string Name,
    string? Description,
    short DurationDays,
    decimal Price,
    bool IsActive,
    DateTime CreatedAt);
