namespace GymMaster.API.Infrastructure;
public interface IFoodImageAnalyzer
{
    /// <summary>
    /// Nhan dien TAT CA mon an nhin thay trong anh, kem uoc luong dinh duong/100g cho moi mon.
    /// </summary>
    Task<FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>> DetectFoodsAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken);
}

/// <summary>Mot mon an Gemini nhan dien duoc + uoc luong dinh duong cho 100g.</summary>
public sealed record DetectedFood(
    string FoodName,
    decimal Confidence,
    decimal Calories,
    decimal ProteinG,
    decimal CarbsG,
    decimal FatG,
    decimal EstimatedGrams);

public sealed record FoodImageAnalysisResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static FoodImageAnalysisResult<T> Success(T value)
        => new(true, value, null, null);

    public static FoodImageAnalysisResult<T> Failure(string code, string message)
        => new(false, default, code, message);
}
