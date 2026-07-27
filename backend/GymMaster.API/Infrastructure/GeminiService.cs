using System.Net.Http.Json;
using System.Text.Json;
using GymMaster.API.Options;
using Microsoft.Extensions.Options;
using GymMaster.API.Common;

namespace GymMaster.API.Infrastructure;

/// <summary>
/// Adapter gọi Google Gemini cho API 09–10.
/// Class dựng prompt + JSON response schema, gửi HTTP request, ghép các phần text,
/// bỏ markdown fence, parse structured output và chuẩn hóa lỗi về FoodImageAnalysisResult.
/// FoodScanService chỉ phụ thuộc IFoodImageAnalyzer nên có thể mock class này trong test.
/// </summary>
public sealed class GeminiService : IFoodImageAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
    }

    // API 09 sử dụng.
    // Dựng schema mảng món, gửi ảnh base64 + prompt cho Gemini,
    // sau đó parse từng phần tử thành DetectedFood và chặn giá trị số âm.
    public async Task<FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>> DetectFoodsAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var schema = new
        {
            type = "ARRAY",
            items = new
            {
                type = "OBJECT",
                properties = new
                {
                    foodName = new { type = "STRING" },
                    confidence = new { type = "NUMBER" },
                    estimatedGrams = new { type = "NUMBER" },
                    calories = new { type = "NUMBER" },
                    proteinG = new { type = "NUMBER" },
                    carbsG = new { type = "NUMBER" },
                    fatG = new { type = "NUMBER" }
                },
                required = new[] { "foodName", "confidence", "estimatedGrams", "calories", "proteinG", "carbsG", "fatG" }
            }
        };

        var json = await CallGeminiAsync(
            imageBytes,
            contentType,
            "Phan tich anh va liet ke TUNG THANH PHAN / NGUYEN LIEU thuc pham nhin thay rieng le " +
            "(vi du mon phuc hop thi tach ra: banh mi, thit, rau, pho mai... moi thu 1 dong). " +
            "Voi MOI thanh phan, tra ve: foodName (ten ngan gon tieng Viet), confidence (0..1), " +
            "estimatedGrams (uoc luong KHOI LUONG cua thanh phan do nhin thay trong anh, tinh bang gram), " +
            "va dinh duong cho 100 gram gom calories, proteinG, carbsG, fatG. " +
            "Tra ve mot mang JSON. Neu khong thay thuc pham nao, tra ve mang rong.",
            schema,
            cancellationToken);

        if (!json.Succeeded)
        {
            return FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(
                json.ErrorCode!, json.ErrorMessage!);
        }

        try
        {
            using var document = json.Value!;
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                return FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI tra ve du lieu khong hop le.");
            }

            var foods = new List<DetectedFood>();
            foreach (var element in root.EnumerateArray())
            {
                var name = element.TryGetProperty("foodName", out var n) ? n.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foods.Add(new DetectedFood(
                    name,
                    ReadDecimal(element, "confidence"),
                    ReadDecimal(element, "calories"),
                    ReadDecimal(element, "proteinG"),
                    ReadDecimal(element, "carbsG"),
                    ReadDecimal(element, "fatG"),
                    ReadDecimal(element, "estimatedGrams")));
            }

            if (foods.Count == 0)
            {
                return FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(
                    "FOOD_NOT_RECOGNIZED", "Khong nhan dien duoc mon an nao trong anh.");
            }

            return FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Success(foods);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(exception, "Invalid Gemini food detection response.");
            return FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(
                "INVALID_AI_RESPONSE", "Dich vu AI tra ve ket qua khong hop le.");
        }
    }

    // API 10 sử dụng.
    // Dựng schema một món, gửi prompt chỉ chứa tên thực phẩm và parse
    // calo/protein/carb/fat trung bình trên 100g thành EstimatedFoodNutrition.
    public async Task<FoodImageAnalysisResult<EstimatedFoodNutrition>> EstimateNutritionAsync(
        string foodName,
        CancellationToken cancellationToken)
    {
        var schema = new
        {
            type = "OBJECT",
            properties = new
            {
                foodName = new { type = "STRING" },
                confidence = new { type = "NUMBER" },
                calories = new { type = "NUMBER" },
                proteinG = new { type = "NUMBER" },
                carbsG = new { type = "NUMBER" },
                fatG = new { type = "NUMBER" }
            },
            required = new[] { "foodName", "confidence", "calories", "proteinG", "carbsG", "fatG" }
        };

        var json = await CallGeminiAsync(
            null,
            null,
            $"Uoc luong dinh duong trung binh cho mot thanh phan thuc pham ten la: '{foodName}'. " +
            "Chi xu ly dung mot ten chung nay, khong tach thanh cac bo phan hay bien the. " +
            "Tra ve foodName (ten ngan gon tieng Viet), confidence (0..1), va gia tri cho 100 gram " +
            "gom calories, proteinG, carbsG, fatG. Cac gia tri phai la so khong am.",
            schema,
            cancellationToken);

        if (!json.Succeeded)
        {
            return FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                json.ErrorCode!, json.ErrorMessage!);
        }

        try
        {
            using var document = json.Value!;
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI tra ve du lieu khong hop le.");
            }

            var normalizedName = root.TryGetProperty("foodName", out var nameElement)
                ? nameElement.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI khong tra ve ten thuc pham hop le.");
            }

            var estimate = new EstimatedFoodNutrition(
                normalizedName,
                ReadDecimal(root, "confidence"),
                ReadDecimal(root, "calories"),
                ReadDecimal(root, "proteinG"),
                ReadDecimal(root, "carbsG"),
                ReadDecimal(root, "fatG"));

            if (estimate.Calories < 0 || estimate.ProteinG < 0 ||
                estimate.CarbsG < 0 || estimate.FatG < 0)
            {
                return FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI tra ve dinh duong khong hop le.");
            }

            return FoodImageAnalysisResult<EstimatedFoodNutrition>.Success(estimate);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(exception, "Invalid Gemini text nutrition response.");
            return FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                "INVALID_AI_RESPONSE", "Dich vu AI tra ve ket qua khong hop le.");
        }
    }

    // Hàm HTTP dùng chung cho cả nhận diện ảnh và ước lượng theo tên:
    // - kiểm tra ApiKey;
    // - gắn prompt và ảnh (nếu có);
    // - yêu cầu Gemini trả JSON theo schema;
    // - xử lý timeout/network/status lỗi;
    // - trích text, bỏ ```json fence và parse JsonDocument.
    private async Task<FoodImageAnalysisResult<JsonDocument>> CallGeminiAsync(
        byte[]? imageBytes,
        string? contentType,
        string prompt,
        object schema,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "AI_NOT_CONFIGURED", "Tinh nang AI chua duoc cau hinh API key.");
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        var parts = new List<object> { new { text = prompt } };
        if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(contentType))
        {
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = contentType,
                    data = Convert.ToBase64String(imageBytes)
                }
            });
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = parts.ToArray()
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schema,
                temperature = 0.2,
                // gemini-2.5-flash bat "thinking" mac dinh -> voi anh phuc tap token suy nghi
                // co the an het budget khien response rong. Tat thinking + noi rong token cho on dinh.
                maxOutputTokens = 4096,
                thinkingConfig = new { thinkingBudget = 0 }
            }
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini returned {StatusCode}.", response.StatusCode);
                return FoodImageAnalysisResult<JsonDocument>.Failure(
                    "RECOGNITION_UNAVAILABLE", "Dich vu AI dang tam thoi khong kha dung.");
            }

            using var payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var (outputText, finishReason) = ExtractOutputText(payload.RootElement);
            WriteDiag($"HTTP 200 finishReason={finishReason} textLen={outputText?.Length ?? 0}");

            if (string.IsNullOrWhiteSpace(outputText))
            {
                // Khong co text (vd finishReason=MAX_TOKENS / SAFETY) -> ghi nguyen response de soi.
                WriteDiag("EMPTY_TEXT raw=" + Truncate(payload.RootElement.GetRawText(), 1500));
                return FoodImageAnalysisResult<JsonDocument>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI khong tra ve noi dung nhan dien.");
            }

            // Gemini doi khi boc JSON trong ```json ... ``` -> go bo truoc khi parse.
            var cleaned = StripJsonFence(outputText);
            try
            {
                return FoodImageAnalysisResult<JsonDocument>.Success(JsonDocument.Parse(cleaned));
            }
            catch (JsonException)
            {
                WriteDiag($"PARSE_FAIL finishReason={finishReason} text={Truncate(cleaned, 1500)}");
                return FoodImageAnalysisResult<JsonDocument>.Failure(
                    "INVALID_AI_RESPONSE", "Dich vu AI tra ve JSON khong hop le (co the bi cat ngan).");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "RECOGNITION_TIMEOUT", "Xu ly AI qua thoi gian cho. Vui long thu lai.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Gemini request failed.");
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "RECOGNITION_UNAVAILABLE", "Khong the ket noi dich vu AI.");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Gemini returned invalid JSON.");
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "INVALID_AI_RESPONSE", "Dich vu AI tra ve phan hoi khong hop le.");
        }
    }

    /// <summary>
    /// Ghép tất cả candidates[*].content.parts[*].text.
    /// Gemini có thể chia JSON dài thành nhiều part; chỉ đọc part đầu sẽ làm JSON bị cắt.
    /// Đồng thời trả finishReason để chẩn đoán MAX_TOKENS/SAFETY.
    /// </summary>
    private static (string? Text, string? FinishReason) ExtractOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return (null, "NO_CANDIDATES");
        }

        var sb = new System.Text.StringBuilder();
        string? finishReason = null;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.TryGetProperty("finishReason", out var fr))
            {
                finishReason = fr.GetString();
            }

            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    sb.Append(text.GetString());
                }
            }
        }

        return (sb.Length > 0 ? sb.ToString() : null, finishReason);
    }

    /// <summary>Bỏ cặp ```json ... ``` nếu Gemini bọc JSON trong Markdown.</summary>
    private static string StripJsonFence(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```"))
        {
            var firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0)
            {
                t = t[(firstNewline + 1)..];
            }
            if (t.EndsWith("```"))
            {
                t = t[..^3];
            }
        }
        return t.Trim();
    }

    // Giới hạn độ dài nội dung ghi log để không làm log chẩn đoán quá lớn.
    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    /// <summary>Ghi chẩn đoán tạm vào gemini_diag.log khi Gemini trả response lỗi.</summary>
    private static void WriteDiag(string line)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gemini_diag.log");
            System.IO.File.AppendAllText(path, $"{AppClock.NowVn():HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch { /* khong de logging lam vo luong */ }
    }

    /// <summary>Đọc số an toàn vì Gemini đôi khi trả number dưới dạng JSON string.</summary>
    private static decimal ReadDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return 0m;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(
                element.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => 0m
        };
    }
}
