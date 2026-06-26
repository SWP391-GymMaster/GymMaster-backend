namespace GymMaster.API.Options;

/// <summary>
/// Cau hinh Gemini Vision cho tinh nang quet anh mon an.
/// API key doc tu appsettings ("Gemini:ApiKey") hoac User Secrets/env — KHONG hard-code.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string Model { get; set; } = "gemini-2.5-flash";

    public string ApiKey { get; set; } = string.Empty;

    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    public int TimeoutSeconds { get; set; } = 20;
}
