using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Nutrition;
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

    // FR-CAL-TGT-01
    [HttpPost("{id:long}/calorie-target")]
    public async Task<IActionResult> SetTarget(
        long id,
        [FromBody] SetCalorieTargetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.SetTargetAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-CAL-TGT-02
    [HttpGet("{id:long}/calorie-target")]
    public async Task<IActionResult> GetTarget(long id, CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetTargetAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-CAL-01
    [HttpGet("{id:long}/calorie-summary")]
    public async Task<IActionResult> GetSummary(
        long id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await _nutritionService.GetSummaryAsync(id, date, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-CAL-01
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
