using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Billing;
public interface IMembershipPackageService
{
    Task<ServiceResult<PackageResponse>> CreateAsync(
        CreatePackageRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<PackageResponse>> UpdateAsync(
        long id,
        UpdatePackageRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<PackageResponse>>> ListAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
