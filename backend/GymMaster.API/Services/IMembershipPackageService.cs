using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IMembershipPackageService
{
    Task<AuthServiceResult<PackageResponse>> CreateAsync(
        CreatePackageRequest request,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<PackageResponse>> UpdateAsync(
        long id,
        UpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<PackageResponse>>> ListAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
