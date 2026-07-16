namespace GymMaster.API.Features.Nutrition;
// DTO rieng cho tinh nang Quet anh AI (tach khoi NutritionDtos de khong dung cham code khac).

/// <summary>Mon ăn tra ve khi khop database (dung cho FE hien + ghi nhat ky).</summary>
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

/// <summary>Nhap dinh duong AI uoc luong (mon chua co trong DB, can user xac nhan).</summary>
public sealed record FoodNutritionDraft(
    string Name,
    string Unit,
    decimal ServingSize,
    decimal CaloriesPerUnit,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    string Source);

/// <summary>Mot mon AI nhan dien: hoac khop DB (Food), hoac la nhap AI (Draft).</summary>
public sealed record FoodScanItem(
    string RecognizedName,
    decimal Confidence,
    string ResultSource,          // "Database" | "AI"
    bool RequiresConfirmation,
    ScannedFood? Food,
    FoodNutritionDraft? Draft,
    decimal EstimatedGrams = 0);  // AI uoc luong khoi luong thanh phan trong anh (gram)

/// <summary>Quet 1 anh co the ra NHIEU mon.</summary>
public sealed record FoodScanResponse(IReadOnlyList<FoodScanItem> Items);

/// <summary>Body khi user xac nhan luu mon AI vao DB.</summary>
public sealed record ConfirmAiFoodRequest(
    string Name,
    decimal CaloriesPerUnit,
    decimal? ProteinG,
    decimal? CarbsG,
    decimal? FatG);
