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

    // Spec 005 — PT tu check-in cho hoi vien duoc phan cong cho minh.
    Task<AuthServiceResult<CheckInResponse>> CreateForAssignedMemberAsync(
        long memberId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    // Danh sach check-in HOM NAY cua cac hoi vien duoc phan cong cho PT (de hien trang thai).
    Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListTodayForTrainerAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}
