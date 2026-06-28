using System.Net.Http.Json;
using System.Text.Json;
using GymMaster.API.Options;
using Microsoft.Extensions.Options;

namespace GymMaster.API.Services;

/// <summary>
/// Goi Gemini Vision (Generative Language API) de nhan dien TAT CA mon an trong anh
/// va uoc luong dinh duong/100g cho moi mon, tra ve trong 1 lan goi (structured output).
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

    // FR-IMG-01/02: nhan dien nhieu mon + uoc luong dinh duong.
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
                    calories = new { type = "NUMBER" },
                    proteinG = new { type = "NUMBER" },
                    carbsG = new { type = "NUMBER" },
                    fatG = new { type = "NUMBER" }
                },
                required = new[] { "foodName", "confidence", "calories", "proteinG", "carbsG", "fatG" }
            }
        };

        var json = await CallGeminiAsync(
            imageBytes,
            contentType,
            "Nhan dien TAT CA cac mon an/thuc pham nhin thay trong anh (co the nhieu mon). " +
            "Voi MOI mon, tra ve: foodName (ten ngan gon bang tieng Viet), confidence (0..1), " +
            "va uoc luong cho 100 gram gom calories, proteinG, carbsG, fatG. " +
            "Tra ve mot mang JSON. Neu khong thay mon an nao, tra ve mang rong.",
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
                    ReadDecimal(element, "fatG")));
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

    private async Task<FoodImageAnalysisResult<JsonDocument>> CallGeminiAsync(
        byte[] imageBytes,
        string contentType,
        string prompt,
        object schema,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "AI_NOT_CONFIGURED", "Tinh nang quet anh chua duoc cau hinh API key.");
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = contentType,
                                data = Convert.ToBase64String(imageBytes)
                            }
                        }
                    }
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
                    "RECOGNITION_UNAVAILABLE", "Dich vu nhan dien mon an dang tam thoi khong kha dung.");
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
                "RECOGNITION_TIMEOUT", "Nhan dien anh qua thoi gian cho. Vui long thu lai.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Gemini request failed.");
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "RECOGNITION_UNAVAILABLE", "Khong the ket noi dich vu nhan dien mon an.");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Gemini returned invalid JSON.");
            return FoodImageAnalysisResult<JsonDocument>.Failure(
                "INVALID_AI_RESPONSE", "Dich vu AI tra ve phan hoi khong hop le.");
        }
    }

    /// <summary>
    /// GHEP TAT CA cac phan text trong candidates[*].content.parts[*].text
    /// (anh that output dai Gemini hay chia thanh NHIEU part -> phai noi lai, neu chi lay part dau se cut JSON).
    /// Tra ve (text gop, finishReason) de chan doan.
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

    /// <summary>Bo cap ```json ... ``` (hoac ``` ... ```) neu Gemini boc JSON trong markdown.</summary>
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

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    /// <summary>Ghi 1 dong chan doan ra file de soi khi quet loi (khong phu thuoc console).</summary>
    private static void WriteDiag(string line)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gemini_diag.log");
            System.IO.File.AppendAllText(path, $"{AppClock.NowVn():HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch { /* khong de logging lam vo luong */ }
    }

    /// <summary>Doc so an toan: Gemini doi khi tra number duoi dang string.</summary>
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
