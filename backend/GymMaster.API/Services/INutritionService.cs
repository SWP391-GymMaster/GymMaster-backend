using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface INutritionService
{
    Task<AuthServiceResult<CalorieTargetResponse>> SetTargetAsync(
        long memberId,
        SetCalorieTargetRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<CalorieTargetResponse>> GetTargetAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<MealLogResponse>> CreateMealLogAsync(
        CreateMealLogRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<MealLogResponse>>> GetMealLogsAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<CalorieSummaryResponse>> GetSummaryAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<CalorieSummaryResponse>>> GetHistoryAsync(
        long memberId,
        DateOnly? from,
        DateOnly? to,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
