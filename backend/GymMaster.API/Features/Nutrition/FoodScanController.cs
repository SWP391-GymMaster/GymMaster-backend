using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nhóm API AI dành riêng cho Member có gói tập active.
/// Controller chỉ nhận file/body; FoodScanService kiểm gói, validate dữ liệu,
/// gọi Gemini và đối chiếu/lưu món trong database.
/// Route dùng /api/v1/foods để tách khỏi kho món /api/v1/food-items.
/// </summary>
[ApiController]
[Route("api/v1/foods")]
[Authorize]
public sealed class FoodScanController : ApiControllerBase
{
    private readonly IFoodScanService _foodScanService;

    public FoodScanController(IFoodScanService foodScanService)
    {
        _foodScanService = foodScanService;
    }

    // 09 POST /api/v1/foods/scan-image
    // Mục đích: nhận ảnh JPG/PNG và dùng Gemini nhận diện một hoặc nhiều món.
    // Input: multipart/form-data, field image; giới hạn nghiệp vụ 5MB.
    // Xử lý thật: FoodScanService.ScanImageAsync; response: FoodScanResponse.
    [HttpPost("scan-image")]
    [Authorize(Roles = RoleNames.Member)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> ScanImage(
        [FromForm] IFormFile? image,
        CancellationToken cancellationToken)
    {
        var result = await _foodScanService.ScanImageAsync(image, User, cancellationToken);
        return ToActionResult(result);
    }

    // 10 POST /api/v1/foods/estimate-nutrition
    // Mục đích: nhờ Gemini ước lượng calo/protein/carb/fat trên 100g từ tên món.
    // Input: EstimateFoodNutritionRequest; response: FoodNutritionDraft.
    // Luồng này chỉ trả bản nháp cho form, chưa lưu food_items.
    [HttpPost("estimate-nutrition")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> EstimateNutrition(
        [FromBody] EstimateFoodNutritionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _foodScanService.EstimateNutritionAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // 11 POST /api/v1/foods/confirm-ai-food
    // Mục đích: lưu món AI sau khi Member đã xem và xác nhận thông tin dinh dưỡng.
    // Input: ConfirmAiFoodRequest; nếu tên đã có thì trả món cũ, nếu mới thì tạo
    // FoodItem Source="AI", ghi audit_logs và trả ScannedFood.
    [HttpPost("confirm-ai-food")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> ConfirmAiFood(
        [FromBody] ConfirmAiFoodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _foodScanService.ConfirmAiFoodAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }
}
