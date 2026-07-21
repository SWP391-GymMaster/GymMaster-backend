using System.Net;
using System.Text;
using System.Text.Json;
using GymMaster.API.Infrastructure;
using GymMaster.API.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GymMaster.Api.Tests;

public sealed class GeminiServiceTests
{
    [Fact]
    public async Task EstimateNutritionAsync_sends_text_only_request_and_parses_structured_response()
    {
        var aiPayload = JsonSerializer.Serialize(new
        {
            foodName = "Ga ta",
            confidence = 0.82m,
            calories = 165m,
            proteinG = 31m,
            carbsG = 0m,
            fatG = 3.6m
        });
        var responsePayload = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new { parts = new[] { new { text = aiPayload } } },
                    finishReason = "STOP"
                }
            }
        });
        var handler = new StubHttpMessageHandler(responsePayload);
        using var client = new HttpClient(handler);
        var service = new GeminiService(
            client,
            Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://gemini.test/v1beta",
                Model = "test-model",
                TimeoutSeconds = 5
            }),
            NullLogger<GeminiService>.Instance);

        var result = await service.EstimateNutritionAsync("Ga ta", default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ga ta", result.Value!.FoodName);
        Assert.Equal(165m, result.Value.Calories);
        Assert.Equal(31m, result.Value.ProteinG);
        Assert.Equal(0m, result.Value.CarbsG);
        Assert.Equal(3.6m, result.Value.FatG);
        Assert.NotNull(handler.RequestBody);
        Assert.Contains("Ga ta", handler.RequestBody);
        Assert.DoesNotContain("inline_data", handler.RequestBody);
    }

    private sealed class StubHttpMessageHandler(string responsePayload) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            };
        }
    }
}
