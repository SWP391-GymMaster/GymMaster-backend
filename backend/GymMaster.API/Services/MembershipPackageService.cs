using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Services;

public sealed class MembershipPackageService : IMembershipPackageService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public MembershipPackageService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-PKG-01
    public async Task<AuthServiceResult<PackageResponse>> CreateAsync(
        CreatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var name = Normalize(request.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Fail<PackageResponse>("VALIDATION_ERROR", "Vui long nhap ten goi.", StatusCodes.Status400BadRequest);
        }

        if (request.DurationDays <= 0)
        {
            return Fail<PackageResponse>("VALIDATION_ERROR", "Thoi han goi phai lon hon 0.", StatusCodes.Status400BadRequest);
        }

        if (request.Price < 0)
        {
            return Fail<PackageResponse>("VALIDATION_ERROR", "Gia goi khong hop le.", StatusCodes.Status400BadRequest);
        }

        if (await _dbContext.MembershipPackages.AnyAsync(item => item.Name == name, cancellationToken))
        {
            return Fail<PackageResponse>("DUPLICATE", "Ten goi nay da ton tai.", StatusCodes.Status409Conflict);
        }

        var package = new MembershipPackage
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            DurationDays = request.DurationDays,
            Price = request.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.MembershipPackages.Add(package);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_PACKAGE", "MembershipPackage", package.Id, null, cancellationToken);

        return AuthServiceResult<PackageResponse>.Success(ToResponse(package), StatusCodes.Status201Created);
    }

    // FR-PKG-01
    public async Task<AuthServiceResult<PackageResponse>> UpdateAsync(
        long id,
        UpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var package = await _dbContext.MembershipPackages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (package is null)
        {
            return Fail<PackageResponse>("NOT_FOUND", "Khong tim thay goi tap.", StatusCodes.Status404NotFound);
        }

        if (request.Name is not null)
        {
            var name = Normalize(request.Name);

            if (string.IsNullOrWhiteSpace(name))
            {
                return Fail<PackageResponse>("VALIDATION_ERROR", "Vui long nhap ten goi.", StatusCodes.Status400BadRequest);
            }

            if (await _dbContext.MembershipPackages.AnyAsync(
                    item => item.Name == name && item.Id != id, cancellationToken))
            {
                return Fail<PackageResponse>("DUPLICATE", "Ten goi nay da ton tai.", StatusCodes.Status409Conflict);
            }

            package.Name = name;
        }

        if (request.DurationDays is not null)
        {
            if (request.DurationDays <= 0)
            {
                return Fail<PackageResponse>("VALIDATION_ERROR", "Thoi han goi phai lon hon 0.", StatusCodes.Status400BadRequest);
            }

            package.DurationDays = request.DurationDays.Value;
        }

        if (request.Price is not null)
        {
            if (request.Price < 0)
            {
                return Fail<PackageResponse>("VALIDATION_ERROR", "Gia goi khong hop le.", StatusCodes.Status400BadRequest);
            }

            package.Price = request.Price.Value;
        }

        if (request.Description is not null)
        {
            package.Description = NormalizeOptional(request.Description);
        }

        if (request.IsActive is not null)
        {
            package.IsActive = request.IsActive.Value;
        }

        package.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("UPDATE_PACKAGE", "MembershipPackage", package.Id, null, cancellationToken);

        return AuthServiceResult<PackageResponse>.Success(ToResponse(package));
    }

    // FR-PKG-03
    public async Task<AuthServiceResult<IReadOnlyList<PackageResponse>>> ListAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.MembershipPackages.AsQueryable();

        if (!principal.IsInRole(RoleNames.Admin) && !principal.IsInRole(RoleNames.Staff))
        {
            query = query.Where(item => item.IsActive);
        }

        var packages = await query
            .OrderBy(item => item.Price)
            .ThenBy(item => item.DurationDays)
            .ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<PackageResponse>>.Success(
            packages.Select(ToResponse).ToList());
    }

    private static PackageResponse ToResponse(MembershipPackage package)
    {
        return new PackageResponse(
            package.Id,
            package.Name,
            package.Description,
            package.DurationDays,
            package.Price,
            package.IsActive,
            package.CreatedAt);
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
