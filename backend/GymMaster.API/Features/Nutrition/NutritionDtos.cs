namespace GymMaster.API.Features.Nutrition;
public sealed record CreateFoodItemRequest(
    string Name,
    string Unit,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

public sealed record FoodItemResponse(
    long Id,
    string Name,
    string Unit,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG,
    bool IsActive);

public sealed record SetCalorieTargetRequest(
    DateOnly? EffectiveDate,
    decimal DailyCalories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

public sealed record CalorieTargetResponse(
    long Id,
    long MemberId,
    DateOnly EffectiveDate,
    decimal DailyCalories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

public sealed record MealItemInput(
    long FoodItemId,
    decimal Quantity);

public sealed record CreateMealLogRequest(
    long MemberId,
    DateOnly LogDate,
    byte MealType,
    IReadOnlyList<MealItemInput> Items);

public sealed record MealItemResponse(
    long Id,
    long FoodItemId,
    string FoodName,
    decimal Quantity,
    decimal Calories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

public sealed record MealLogResponse(
    long Id,
    long MemberId,
    DateOnly LogDate,
    string MealType,
    IReadOnlyList<MealItemResponse> Items,
    decimal TotalCalories);

public sealed record CalorieSummaryResponse(
    DateOnly Date,
    decimal Consumed,
    decimal? Target,
    decimal? Remaining,
    decimal ConsumedProteinG = 0,
    decimal ConsumedCarbG = 0,
    decimal ConsumedFatG = 0,
    decimal? TargetProteinG = null,
    decimal? TargetCarbG = null,
    decimal? TargetFatG = null,
    decimal? RemainingProteinG = null,
    decimal? RemainingCarbG = null,
    decimal? RemainingFatG = null);
