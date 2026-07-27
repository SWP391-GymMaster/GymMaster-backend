namespace GymMaster.API.Features.Nutrition;

// DTO riêng cho API 09–11 của tính năng AI; tách khỏi NutritionDtos
// để thay đổi contract AI không ảnh hưởng contract nhật ký ăn/calo.

/// <summary>
/// Món hoàn chỉnh đã có Id trong database; frontend có thể chọn ngay để ghi nhật ký.
/// </summary>
public sealed record ScannedFood(
    long Id,
    string Name,
    string Unit,
    decimal ServingSize,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG,
    string Source);

/// <summary>
/// Bản nháp dinh dưỡng Gemini ước lượng cho món chưa có trong database.
/// Member phải kiểm tra và gọi API 11 xác nhận trước khi món được lưu.
/// </summary>
public sealed record FoodNutritionDraft(
    string Name,
    string Unit,
    decimal ServingSize,
    decimal CaloriesPerUnit,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    string Source);

/// <summary>
/// Một kết quả trong API 09: hoặc Food đã match database,
/// hoặc Draft do AI tạo và RequiresConfirmation=true.
/// </summary>
public sealed record FoodScanItem(
    string RecognizedName,
    decimal Confidence,
    string ResultSource,          // "Database" | "AI"
    bool RequiresConfirmation,
    ScannedFood? Food,
    FoodNutritionDraft? Draft,
    decimal EstimatedGrams = 0);  // AI uoc luong khoi luong thanh phan trong anh (gram)

/// <summary>Response API 09; một ảnh có thể nhận diện nhiều món.</summary>
public sealed record FoodScanResponse(IReadOnlyList<FoodScanItem> Items);

/// <summary>Body API 10: tên món cần Gemini ước lượng dinh dưỡng.</summary>
public sealed record EstimateFoodNutritionRequest(string Name);

/// <summary>
/// Body API 11: dữ liệu Member đã xác nhận để lưu món AI vào food_items.
/// </summary>
public sealed record ConfirmAiFoodRequest(
    string Name,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG);
