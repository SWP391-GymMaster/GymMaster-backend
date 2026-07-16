using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.CheckIns;
public interface ICheckInService
{
    Task<ServiceResult<CheckInResponse>> CreateAsync(
        CreateCheckInRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<CheckInResponse>>> ListAsync(
        DateOnly? date, long? memberId, CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<CheckInResponse>>> ListByMemberAsync(
        long memberId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    // Spec 005 — PT tu check-in cho hoi vien duoc phan cong cho minh.
    Task<ServiceResult<CheckInResponse>> CreateForAssignedMemberAsync(
        long memberId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    // Danh sach check-in HOM NAY cua cac hoi vien duoc phan cong cho PT (de hien trang thai).
    Task<ServiceResult<IReadOnlyList<CheckInResponse>>> ListTodayForTrainerAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}
