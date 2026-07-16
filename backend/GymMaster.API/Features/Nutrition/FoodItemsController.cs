using GymMaster.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;
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

    // FR-FOOD-01
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

    // FR-FOOD-02
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
