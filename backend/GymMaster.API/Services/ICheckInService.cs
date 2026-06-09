using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface ICheckInService
{
    Task<AuthServiceResult<CheckInResponse>> CreateAsync(
        CreateCheckInRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListAsync(
        DateOnly? date, long? memberId, CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListByMemberAsync(
        long memberId, ClaimsPrincipal principal, CancellationToken cancellationToken);
}
