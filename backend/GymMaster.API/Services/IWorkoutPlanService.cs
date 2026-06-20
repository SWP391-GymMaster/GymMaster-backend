using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IWorkoutPlanService
{
    Task<AuthServiceResult<WorkoutPlanResponse>> CreateAsync(
        long memberId,
        CreateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<WorkoutPlanResponse>> UpdateAsync(
        long planId,
        UpdateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<WorkoutPlanResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<object?>> DeleteAsync(
        long planId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
