namespace GymMaster.API.Infrastructure;

/// <summary>
/// Hợp đồng gọi dịch vụ AI mà FoodScanService sử dụng.
/// Tách interface giúp unit test mock AI mà không gọi Gemini thật.
/// </summary>
public interface IFoodImageAnalyzer
{
    /// <summary>
    /// API 09 sử dụng: nhận diện tất cả món nhìn thấy trong ảnh,
    /// kèm độ tin cậy, khối lượng và dinh dưỡng ước lượng.
    /// </summary>
    Task<FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>> DetectFoodsAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// API 10 sử dụng: ước lượng dinh dưỡng trung bình trên 100g
    /// từ một tên thực phẩm, không cần ảnh.
    /// </summary>
    Task<FoodImageAnalysisResult<EstimatedFoodNutrition>> EstimateNutritionAsync(
        string foodName,
        CancellationToken cancellationToken);
}

/// <summary>Một món Gemini nhận diện trong ảnh và các số liệu AI ước lượng.</summary>
public sealed record DetectedFood(
    string FoodName,
    decimal Confidence,
    decimal Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal EstimatedGrams);

/// <summary>Kết quả Gemini ước lượng dinh dưỡng từ tên món cho API 10.</summary>
public sealed record EstimatedFoodNutrition(
    string FoodName,
    decimal Confidence,
    decimal Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG);

/// <summary>
/// Bao kết quả gọi AI: thành công có Value; thất bại có ErrorCode/ErrorMessage.
/// FoodScanService chuyển lỗi này thành ServiceResult và HTTP 502.
/// </summary>
public sealed record FoodImageAnalysisResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>Tạo kết quả AI thành công.</summary>
    public static FoodImageAnalysisResult<T> Success(T value)
        => new(true, value, null, null);

    /// <summary>Tạo kết quả AI thất bại mà không ném exception nghiệp vụ.</summary>
    public static FoodImageAnalysisResult<T> Failure(string code, string message)
        => new(false, default, code, message);
}
