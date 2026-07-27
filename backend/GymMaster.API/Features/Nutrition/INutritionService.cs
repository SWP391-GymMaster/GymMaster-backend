using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Hợp đồng nghiệp vụ mục tiêu calo, nhật ký ăn và tổng kết dinh dưỡng.
/// MemberNutritionController và MealLogsController cùng gọi interface này.
/// ClaimsPrincipal được truyền xuống để service kiểm quyền theo role và ownership.
/// </summary>
public interface INutritionService
{
    // API 01: tạo/cập nhật mục tiêu theo MemberId + EffectiveDate.
    Task<ServiceResult<CalorieTargetResponse>> SetTargetAsync(
        long memberId,
        SetCalorieTargetRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 02: lấy mục tiêu mới nhất đã có hiệu lực.
    Task<ServiceResult<CalorieTargetResponse>> GetTargetAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 05: tạo hoặc cộng dồn bữa ăn và ghi Audit Log.
    Task<ServiceResult<MealLogResponse>> CreateMealLogAsync(
        CreateMealLogRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 06: lấy tất cả bữa của hội viên trong một ngày.
    Task<ServiceResult<IReadOnlyList<MealLogResponse>>> GetMealLogsAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 03: tính consumed/target/remaining của calo và macro trong một ngày.
    Task<ServiceResult<CalorieSummaryResponse>> GetSummaryAsync(
        long memberId,
        DateOnly? date,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    // API 04: tổng hợp calo từng ngày trong một khoảng để frontend vẽ biểu đồ.
    Task<ServiceResult<IReadOnlyList<CalorieSummaryResponse>>> GetHistoryAsync(
        long memberId,
        DateOnly? from,
        DateOnly? to,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
