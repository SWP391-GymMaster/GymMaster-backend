using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Nhóm API kho món ăn dùng cho ô tìm món và form tạo món thủ công.
/// Controller nhận HTTP request; FoodItemService mới là nơi áp giới hạn kho miễn phí,
/// tìm kiếm, validation, chống trùng và ghi database.
/// </summary>
[ApiController]
[Route("api/v1/food-items")]
[Authorize]
public sealed class FoodItemsController : ApiControllerBase
{
    private readonly IFoodItemService _foodItemService;

    public FoodItemsController(IFoodItemService foodItemService)
    {
        _foodItemService = foodItemService;
    }

    // 07 GET /api/v1/food-items?query=&page=&pageSize=
    // Mục đích: tìm món active theo tên và trả kết quả phân trang cho frontend.
    // Member có gói active được xem toàn kho; member free chỉ tìm trong 20 món dùng thử.
    // Xử lý thật: FoodItemService.SearchAsync; response: PagedResult<FoodItemResponse>.
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _foodItemService.SearchAsync(query, page, pageSize, User, cancellationToken);
        return ToActionResult(result);
    }

    // 08 POST /api/v1/food-items
    // Mục đích: tạo món thủ công từ CreateFoodItemRequest.
    // Nếu trùng tên thì trả món có sẵn; nếu mới thì ghi food_items và audit_logs.
    // Xử lý thật: FoodItemService.AddAsync; response: FoodItemResponse.
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Member},{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> Add(
        [FromBody] CreateFoodItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _foodItemService.AddAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }
}
