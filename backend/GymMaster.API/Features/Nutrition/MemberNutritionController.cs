using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nhóm API dinh dưỡng theo một hội viên cụ thể.
/// Controller chỉ nhận route/query/body, chuyển sang INutritionService và trả HTTP response;
/// toàn bộ kiểm tra quyền, validation và tính toán nằm trong NutritionService.
/// </summary>
[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MemberNutritionController : ApiControllerBase
{
    private readonly INutritionService _nutritionService;

    public MemberNutritionController(INutritionService nutritionService)
    {
        _nutritionService = nutritionService;
    }

    // 01 POST /api/v1/members/{id}/calorie-target
    // Mục đích: đặt mới hoặc cập nhật mục tiêu calo và macro của hội viên.
    // Input: id = MemberProfile.Id; body SetCalorieTargetRequest chứa ngày hiệu lực,
    // DailyCalories, ProteinG, CarbG và FatG.
    // Xử lý thật: NutritionService.SetTargetAsync; response: CalorieTargetResponse.
    [HttpPost("{id:long}/calorie-target")]
    public async Task<IActionResult> SetTarget(
        long id,
        [FromBody] SetCalorieTargetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.SetTargetAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // 02 GET /api/v1/members/{id}/calorie-target
    // Mục đích: lấy mục tiêu calo gần nhất đang có hiệu lực của hội viên.
    // Xử lý thật: NutritionService.GetTargetAsync; response: CalorieTargetResponse.
    // Nếu chưa từng đặt mục tiêu phù hợp thì service trả 404 NO_TARGET.
    [HttpGet("{id:long}/calorie-target")]
    public async Task<IActionResult> GetTarget(long id, CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetTargetAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // 03 GET /api/v1/members/{id}/calorie-summary?date=
    // Mục đích: tổng hợp calo và protein/carb/fat đã ăn, mục tiêu và phần còn lại trong một ngày.
    // Query date không truyền thì service dùng ngày hiện tại theo giờ Việt Nam.
    // Xử lý thật: NutritionService.GetSummaryAsync; response: CalorieSummaryResponse.
    [HttpGet("{id:long}/calorie-summary")]
    public async Task<IActionResult> GetSummary(
        long id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetSummaryAsync(id, date, User, cancellationToken);
        return ToActionResult(result);
    }

    // 04 GET /api/v1/members/{id}/calorie-history?from=&to=
    // Mục đích: lấy lịch sử calo theo từng ngày để frontend vẽ biểu đồ.
    // Không truyền khoảng ngày thì service mặc định lấy 7 ngày kết thúc ở hôm nay.
    // Xử lý thật: NutritionService.GetHistoryAsync; response: danh sách CalorieSummaryResponse.
    [HttpGet("{id:long}/calorie-history")]
    public async Task<IActionResult> GetHistory(
        long id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetHistoryAsync(id, from, to, User, cancellationToken);
        return ToActionResult(result);
    }
}
