namespace GymMaster.API.Features.Billing;
public sealed record CreatePackageRequest(
    string Name,
    short DurationDays,
    decimal Price,
    string? Description,
    bool SupportsPT = false);

public sealed record UpdatePackageRequest(
    string? Name,
    short? DurationDays,
    decimal? Price,
    string? Description,
    bool? IsActive,
    bool? SupportsPT);

public sealed record PackageResponse(
    long Id,
    string Name,
    string? Description,
    short DurationDays,
    decimal Price,
    bool IsActive,
    bool SupportsPT,
    DateTime CreatedAt);
