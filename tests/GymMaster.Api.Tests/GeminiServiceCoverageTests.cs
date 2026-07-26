using System.Net;
using System.Text;
using System.Text.Json;
using GymMaster.API.Infrastructure;
using GymMaster.API.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymMaster.Api.Tests;

// Phu logic parse + cac nhanh loi cua GeminiService bang HttpMessageHandler gia
// (khong goi mang that, khong ton API key).
public class GeminiServiceCoverageTests
{
    // Boc text (chinh la JSON AI tra) vao dung phong bi candidates cua Gemini.
    private static string Envelope(string innerText, string finishReason = "STOP")
        => JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = innerText } } }, finishReason },
            },
        });

    private static GeminiService Service(HttpMessageHandler handler, string apiKey = "test-key")
        => new(
            new HttpClient(handler),
            Options.Create(new GeminiOptions { ApiKey = apiKey, BaseUrl = "https://gemini.test/v1beta", Model = "m", TimeoutSeconds = 5 }),
            NullLogger<GeminiService>.Instance);

    private sealed class Stub : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _throw;
        public string? RequestBody { get; private set; }

        public Stub(string body, HttpStatusCode status = HttpStatusCode.OK) { _body = body; _status = status; }
        public Stub(Exception toThrow) { _throw = toThrow; _body = ""; _status = HttpStatusCode.OK; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            if (_throw is not null) throw _throw;
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly byte[] Image = new byte[] { 1, 2, 3 };

    // ---------- DetectFoodsAsync ----------

    [Fact] // Nhan dien nhieu mon + gui kem anh (inline_data)
    public async Task Detect_success_parses_foods_and_sends_image()
    {
        var foods = "[{\"foodName\":\"Com\",\"confidence\":0.9,\"estimatedGrams\":200,\"calories\":130,\"proteinG\":2.7,\"carbsG\":28,\"fatG\":0.3}]";
        var stub = new Stub(Envelope(foods));
        var service = Service(stub);

        var result = await service.DetectFoodsAsync(Image, "image/png", default);

        Assert.True(result.Succeeded);
        var food = Assert.Single(result.Value!);
        Assert.Equal("Com", food.FoodName);
        Assert.Equal(130m, food.Calories);
        Assert.Contains("inline_data", stub.RequestBody);
    }

    [Fact] // Gemini boc JSON trong ```json ... ``` -> van parse duoc (StripJsonFence)
    public async Task Detect_strips_markdown_fence()
    {
        var fenced = "```json\n[{\"foodName\":\"Pho\",\"confidence\":0.8,\"estimatedGrams\":300,\"calories\":90,\"proteinG\":5,\"carbsG\":12,\"fatG\":2}]\n```";
        var service = Service(new Stub(Envelope(fenced)));

        var result = await service.DetectFoodsAsync(Image, "image/png", default);

        Assert.True(result.Succeeded);
        Assert.Equal("Pho", result.Value![0].FoodName);
    }

    [Fact] // So duoi dang chuoi -> ReadDecimal van parse
    public async Task Detect_reads_numbers_given_as_strings()
    {
        var foods = "[{\"foodName\":\"Ga\",\"confidence\":\"0.7\",\"estimatedGrams\":\"150\",\"calories\":\"165\",\"proteinG\":\"31\",\"carbsG\":\"0\",\"fatG\":\"3.6\"}]";
        var service = Service(new Stub(Envelope(foods)));

        var result = await service.DetectFoodsAsync(Image, "image/png", default);

        Assert.True(result.Succeeded);
        Assert.Equal(165m, result.Value![0].Calories);
    }

    [Fact] // Mang rong -> FOOD_NOT_RECOGNIZED
    public async Task Detect_empty_array_returns_not_recognized()
    {
        var service = Service(new Stub(Envelope("[]")));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("FOOD_NOT_RECOGNIZED", result.ErrorCode);
    }

    [Fact] // Khong phai mang -> INVALID_AI_RESPONSE
    public async Task Detect_non_array_returns_invalid()
    {
        var service = Service(new Stub(Envelope("{\"foodName\":\"x\"}")));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    [Fact] // JSON hong trong text -> INVALID_AI_RESPONSE (PARSE_FAIL)
    public async Task Detect_broken_json_returns_invalid()
    {
        var service = Service(new Stub(Envelope("[{khong-phai-json")));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    [Fact] // Khong co text (candidates rong parts) -> INVALID_AI_RESPONSE
    public async Task Detect_empty_text_returns_invalid()
    {
        var noText = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = Array.Empty<object>() }, finishReason = "MAX_TOKENS" } } });
        var service = Service(new Stub(noText));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    // ---------- Nhanh HTTP / cau hinh ----------

    [Fact] // Chua cau hinh API key -> AI_NOT_CONFIGURED
    public async Task No_api_key_returns_not_configured()
    {
        var service = Service(new Stub(Envelope("[]")), apiKey: "");
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("AI_NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact] // HTTP 500 tu Gemini -> RECOGNITION_UNAVAILABLE
    public async Task Http_error_returns_unavailable()
    {
        var service = Service(new Stub("loi", HttpStatusCode.InternalServerError));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("RECOGNITION_UNAVAILABLE", result.ErrorCode);
    }

    [Fact] // Loi ket noi (HttpRequestException) -> RECOGNITION_UNAVAILABLE
    public async Task Connection_error_returns_unavailable()
    {
        var service = Service(new Stub(new HttpRequestException("down")));
        var result = await service.DetectFoodsAsync(Image, "image/png", default);
        Assert.False(result.Succeeded);
        Assert.Equal("RECOGNITION_UNAVAILABLE", result.ErrorCode);
    }

    // ---------- EstimateNutritionAsync ----------

    [Fact] // Uoc luong thanh cong (text-only, khong kem anh)
    public async Task Estimate_success_text_only()
    {
        var payload = "{\"foodName\":\"Ga ta\",\"confidence\":0.8,\"calories\":165,\"proteinG\":31,\"carbsG\":0,\"fatG\":3.6}";
        var stub = new Stub(Envelope(payload));
        var service = Service(stub);

        var result = await service.EstimateNutritionAsync("Ga ta", default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ga ta", result.Value!.FoodName);
        Assert.DoesNotContain("inline_data", stub.RequestBody);
    }

    [Fact] // Gia tri am -> INVALID_AI_RESPONSE
    public async Task Estimate_negative_values_returns_invalid()
    {
        var payload = "{\"foodName\":\"X\",\"confidence\":0.5,\"calories\":-1,\"proteinG\":1,\"carbsG\":1,\"fatG\":1}";
        var service = Service(new Stub(Envelope(payload)));
        var result = await service.EstimateNutritionAsync("X", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    [Fact] // Khong phai object -> INVALID_AI_RESPONSE
    public async Task Estimate_non_object_returns_invalid()
    {
        var service = Service(new Stub(Envelope("[1,2,3]")));
        var result = await service.EstimateNutritionAsync("X", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    [Fact] // Thieu foodName -> INVALID_AI_RESPONSE
    public async Task Estimate_missing_name_returns_invalid()
    {
        var service = Service(new Stub(Envelope("{\"confidence\":0.5,\"calories\":10,\"proteinG\":1,\"carbsG\":1,\"fatG\":1}")));
        var result = await service.EstimateNutritionAsync("X", default);
        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_AI_RESPONSE", result.ErrorCode);
    }

    [Fact] // Chua cau hinh -> AI_NOT_CONFIGURED
    public async Task Estimate_no_api_key()
    {
        var service = Service(new Stub(Envelope("{}")), apiKey: "");
        var result = await service.EstimateNutritionAsync("X", default);
        Assert.False(result.Succeeded);
        Assert.Equal("AI_NOT_CONFIGURED", result.ErrorCode);
    }
}
