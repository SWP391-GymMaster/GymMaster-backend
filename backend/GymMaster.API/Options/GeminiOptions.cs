namespace GymMaster.API.Options;

/// <summary>
/// Cấu hình Gemini cho API 09–10.
/// Được bind từ section "Gemini" trong appsettings/User Secrets/biến môi trường.
/// API key là secret, không hard-code và không commit vào repository.
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>Tên section cấu hình mà Program.cs dùng để bind.</summary>
    public const string SectionName = "Gemini";

    /// <summary>URL gốc của Google Generative Language API.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Model Gemini thực hiện nhận diện/ước lượng.</summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>API key lấy từ secret hoặc biến môi trường.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Dung lượng ảnh tối đa mà FoodScanService chấp nhận: mặc định 5MB.</summary>
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Thời gian tối đa chờ Gemini; GeminiService kẹp trong khoảng 5–60 giây.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}
