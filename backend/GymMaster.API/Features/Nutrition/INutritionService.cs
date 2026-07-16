using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;
public interface INutritionService
{
    Task<ServiceResult<CalorieTargetResponse>> SetTargetAsync(
        long memberId,
        SetCalorieTargetRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<CalorieTargetResponse>> GetTargetAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<MealLogResponse>> CreateMealLogAsync(
        CreateMealLogRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<MealLogResponse>>> GetMealLogsAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<CalorieSummaryResponse>> GetSummaryAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<CalorieSummaryResponse>>> GetHistoryAsync(
        long memberId,
        DateOnly? from,
        DateOnly? to,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
