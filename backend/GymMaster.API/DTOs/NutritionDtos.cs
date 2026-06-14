namespace GymMaster.API.DTOs;

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

// Ket qua tim mon tu nguon online (Open Food Facts). Chua luu vao DB —
// chi de FE hien cho user chon, sau do POST /food-items moi luu.
public sealed record OnlineFoodItemResponse(
    string Name,
    string Unit,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG,
    string Source);

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
    decimal? Remaining);
