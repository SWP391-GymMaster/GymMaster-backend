using GymMaster.API.DTOs;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

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

    // FR-MEAL-01
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMealLogRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.CreateMealLogAsync(request, User, cancellationToken);
        return ToActionResult(result);
    }

    // 🆕 API_CONTRACT_Y: GET /api/v1/meal-logs?memberId=&date= (Member self/Admin/Staff).
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
