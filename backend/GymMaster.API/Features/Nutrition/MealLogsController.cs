using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nhóm API ghi và đọc nhật ký bữa ăn.
/// Controller không tự tính calo; NutritionService chịu trách nhiệm kiểm quyền,
/// gộp món, tính calo/macro và truy vấn database.
/// </summary>
[ApiController]
[Route("api/v1/meal-logs")]
[Authorize]
public sealed class MealLogsController : ApiControllerBase
{
    private readonly INutritionService _nutritionService;

    public MealLogsController(INutritionService nutritionService)
    {
        _nutritionService = nutritionService;
    }

    // 05 POST /api/v1/meal-logs
    // Mục đích: ghi một bữa ăn gồm nhiều món cho hội viên trong một ngày.
    // Input: CreateMealLogRequest chứa MemberId, LogDate, MealType và danh sách MealItemInput.
    // Xử lý thật: NutritionService.CreateMealLogAsync; ghi meal_logs/meal_log_items
    // và audit_logs; response: MealLogResponse.
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMealLogRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.CreateMealLogAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // 06 GET /api/v1/meal-logs?memberId=&date=
    // Mục đích: lấy toàn bộ bữa sáng/trưa/tối/ăn nhẹ của một hội viên trong ngày.
    // Query date không truyền thì service dùng hôm nay theo giờ Việt Nam.
    // Xử lý thật: NutritionService.GetMealLogsAsync; response: danh sách MealLogResponse.
    [HttpGet]
    public async Task<IActionResult> GetByMemberAndDate(
        [FromQuery] long memberId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetMealLogsAsync(memberId, date, User, cancellationToken);
        return ToActionResult(result);
    }
}
