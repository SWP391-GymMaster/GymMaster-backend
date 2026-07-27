namespace GymMaster.API.Features.Nutrition;

/// <summary>
/// Body của API 08 POST /food-items: dữ liệu frontend gửi khi tạo món thủ công.
/// CaloriesPerUnit và các macro được tính theo đơn vị ghi trong Unit.
/// </summary>
public sealed record CreateFoodItemRequest(
    string Name,
    string Unit,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

/// <summary>
/// Món ăn trả về cho API 07/08; chỉ đưa các field frontend cần,
/// không trả trực tiếp entity FoodItem.
/// </summary>
public sealed record FoodItemResponse(
    long Id,
    string Name,
    string Unit,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG,
    bool IsActive);

/// <summary>
/// Body của API 01 POST /members/{id}/calorie-target.
/// EffectiveDate null nghĩa là bắt đầu áp dụng từ hôm nay theo giờ Việt Nam.
/// </summary>
public sealed record SetCalorieTargetRequest(
    DateOnly? EffectiveDate,
    decimal DailyCalories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

/// <summary>
/// Mục tiêu calo/macro trả về sau khi đặt hoặc khi lấy mục tiêu hiện hành.
/// </summary>
public sealed record CalorieTargetResponse(
    long Id,
    long MemberId,
    DateOnly EffectiveDate,
    decimal DailyCalories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

/// <summary>
/// Một món frontend gửi trong API 05 ghi bữa ăn.
/// Quantity là số khẩu phần theo đơn vị dinh dưỡng của FoodItem.
/// </summary>
public sealed record MealItemInput(
    long FoodItemId,
    decimal Quantity);

/// <summary>
/// Body của API 05 POST /meal-logs.
/// MealType dùng byte: 1=Sáng, 2=Trưa, 3=Tối, 4=Ăn nhẹ.
/// </summary>
public sealed record CreateMealLogRequest(
    long MemberId,
    DateOnly LogDate,
    byte MealType,
    IReadOnlyList<MealItemInput> Items);

/// <summary>
/// Chi tiết một món trong MealLogResponse, gồm tên món, lượng,
/// calo và macro đã nhân theo Quantity.
/// </summary>
public sealed record MealItemResponse(
    long Id,
    long FoodItemId,
    string FoodName,
    decimal Quantity,
    decimal Calories,
    decimal? ProteinG,
    decimal? CarbG,
    decimal? FatG);

/// <summary>
/// Một bữa ăn trả về cho API 05/06, gồm các món và tổng calo của bữa.
/// </summary>
public sealed record MealLogResponse(
    long Id,
    long MemberId,
    DateOnly LogDate,
    string MealType,
    IReadOnlyList<MealItemResponse> Items,
    decimal TotalCalories);

/// <summary>
/// Kết quả API 03/04: lượng đã ăn, mục tiêu và phần còn lại của calo/macro.
/// Target/Remaining null khi ngày đó chưa có mục tiêu hiệu lực.
/// </summary>
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
