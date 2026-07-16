using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Training;
public interface IWorkoutPlanService
{
    Task<ServiceResult<WorkoutPlanResponse>> CreateAsync(
        long memberId,
        CreateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<WorkoutPlanResponse>> UpdateAsync(
        long planId,
        UpdateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<WorkoutPlanResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<object?>> DeleteAsync(
        long planId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
