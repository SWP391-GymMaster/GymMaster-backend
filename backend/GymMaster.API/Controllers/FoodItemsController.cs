using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/food-items")]
[Authorize]
public sealed class FoodItemsController : ApiControllerBase
{
    private readonly IFoodItemService _foodItemService;
    private readonly IFoodOnlineSearchService _onlineSearchService;

    public FoodItemsController(
        IFoodItemService foodItemService,
        IFoodOnlineSearchService onlineSearchService)
    {
        _foodItemService = foodItemService;
        _onlineSearchService = onlineSearchService;
    }

    // FR-FOOD-01
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _foodItemService.SearchAsync(query, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    // FR-FOOD (online assist): tim mon tu Open Food Facts de FE hien cho user chon.
    [HttpGet("online-search")]
    public async Task<IActionResult> OnlineSearch(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var items = await _onlineSearchService.SearchAsync(query, cancellationToken);
        return ToActionResult(AuthServiceResult<IReadOnlyList<OnlineFoodItemResponse>>.Success(items));
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
