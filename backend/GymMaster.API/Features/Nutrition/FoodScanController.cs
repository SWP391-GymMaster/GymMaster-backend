using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;
// Tinh nang Quet anh AI — controller rieng, route api/v1/foods (khong dung cham FoodItemsController).
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

    // FR-IMG-01/02: nhan dien nhieu mon trong anh.
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

    // AI ho tro form tu tao: uoc luong dinh duong/100g tu mot ten chung, chua luu DB.
    [HttpPost("estimate-nutrition")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> EstimateNutrition(
        [FromBody] EstimateFoodNutritionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _foodScanService.EstimateNutritionAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-IMG-03: luu mon AI sau khi user xac nhan.
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
