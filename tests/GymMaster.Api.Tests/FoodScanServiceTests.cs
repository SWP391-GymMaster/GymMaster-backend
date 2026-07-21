using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Nutrition;
using GymMaster.API.Infrastructure;
using GymMaster.API.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymMaster.Api.Tests;

// White-box tests: exercise authorization, file-validation, dependency-failure,
// successful empty detection and confirmation-validation branches without calling Gemini.
public sealed class FoodScanServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"food-scan-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Member(long userId = 10)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Member)
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static IFormFile Image(string contentType = "image/jpeg", byte[]? bytes = null)
    {
        bytes ??= [1, 2, 3, 4];
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", "meal.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static async Task SeedActiveMembershipAsync(GymMasterDbContext db, long userId = 10)
    {
        db.Users.Add(new User { Id = userId, Email = $"member{userId}@test.local", FullName = "Member Test" });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = userId });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1,
            Name = "Active package",
            DurationDays = 30,
            Price = 100_000m
        });
        db.Memberships.Add(new Membership
        {
            Id = 1,
            MemberId = 1,
            PackageId = 1,
            StartDate = AppClock.Today().AddDays(-1),
            EndDate = AppClock.Today().AddDays(29),
            Status = MembershipStatus.Active,
            CreatedByUserId = 99
        });
        await db.SaveChangesAsync();
    }

    private static FoodScanService CreateService(GymMasterDbContext db, FakeAnalyzer analyzer, int maxBytes = 5_000_000)
        => new(
            db,
            analyzer,
            new NoopAudit(),
            Options.Create(new GeminiOptions { MaxImageBytes = maxBytes }));

    [Fact]
    public async Task ScanImage_without_active_membership_returns_forbidden_before_analyzer_call()
    {
        await using var db = NewDb();
        var analyzer = FakeAnalyzer.Success([]);
        var service = CreateService(db, analyzer);

        var result = await service.ScanImageAsync(Image(), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBERSHIP_REQUIRED", result.ErrorCode);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public async Task ScanImage_invalid_content_type_returns_unprocessable_entity()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.Success([]);
        var service = CreateService(db, analyzer);

        var result = await service.ScanImageAsync(Image("image/gif"), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_FILE", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public async Task ScanImage_over_size_limit_returns_unprocessable_entity()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.Success([]);
        var service = CreateService(db, analyzer, maxBytes: 3);

        var result = await service.ScanImageAsync(Image(), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_FILE", result.ErrorCode);
        Assert.Equal(0, analyzer.CallCount);
    }

    [Fact]
    public async Task ScanImage_when_analyzer_fails_maps_dependency_error_to_bad_gateway()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.Failure("GEMINI_UNAVAILABLE", "Gemini unavailable");
        var service = CreateService(db, analyzer);

        var result = await service.ScanImageAsync(Image(), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("GEMINI_UNAVAILABLE", result.ErrorCode);
        Assert.Equal(502, result.StatusCode);
        Assert.Equal(1, analyzer.CallCount);
    }

    [Fact]
    public async Task ScanImage_when_analyzer_detects_nothing_returns_empty_success()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.Success([]);
        var service = CreateService(db, analyzer);

        var result = await service.ScanImageAsync(Image(), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(1, analyzer.CallCount);
    }

    [Fact]
    public async Task EstimateNutrition_without_active_membership_returns_forbidden()
    {
        await using var db = NewDb();
        var analyzer = FakeAnalyzer.EstimateSuccess(
            new EstimatedFoodNutrition("Ga ta", 0.8m, 165, 31, 0, 3.6m));
        var service = CreateService(db, analyzer);

        var result = await service.EstimateNutritionAsync(
            new EstimateFoodNutritionRequest("Ga ta"), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("MEMBERSHIP_REQUIRED", result.ErrorCode);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, analyzer.EstimateCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task EstimateNutrition_invalid_name_returns_validation_error(string name)
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.EstimateSuccess(
            new EstimatedFoodNutrition("Ga ta", 0.8m, 165, 31, 0, 3.6m));
        var service = CreateService(db, analyzer);

        var result = await service.EstimateNutritionAsync(
            new EstimateFoodNutritionRequest(name), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, analyzer.EstimateCallCount);
    }

    [Fact]
    public async Task EstimateNutrition_when_gemini_fails_maps_error_to_bad_gateway()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.EstimateFailure("RECOGNITION_UNAVAILABLE", "Gemini unavailable");
        var service = CreateService(db, analyzer);

        var result = await service.EstimateNutritionAsync(
            new EstimateFoodNutritionRequest("Ga ta"), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("RECOGNITION_UNAVAILABLE", result.ErrorCode);
        Assert.Equal(502, result.StatusCode);
        Assert.Equal(1, analyzer.EstimateCallCount);
    }

    [Fact]
    public async Task EstimateNutrition_success_returns_reviewable_draft_without_saving_food()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var analyzer = FakeAnalyzer.EstimateSuccess(
            new EstimatedFoodNutrition("Ga ta", 0.82m, 165, 31, 0, 3.6m));
        var service = CreateService(db, analyzer);

        var result = await service.EstimateNutritionAsync(
            new EstimateFoodNutritionRequest("Ga ta"), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ga ta", result.Value!.Name);
        Assert.Equal("100g", result.Value.Unit);
        Assert.Equal(100m, result.Value.ServingSize);
        Assert.Equal(165m, result.Value.CaloriesPerUnit);
        Assert.Equal(31m, result.Value.ProteinG);
        Assert.Equal(0m, result.Value.CarbsG);
        Assert.Equal(3.6m, result.Value.FatG);
        Assert.Equal("AI", result.Value.Source);
        Assert.Empty(db.FoodItems);
    }

    [Fact]
    public async Task ConfirmAiFood_blank_name_returns_validation_error()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var service = CreateService(db, FakeAnalyzer.Success([]));

        var result = await service.ConfirmAiFoodAsync(
            new ConfirmAiFoodRequest("   ", 100, 10, 20, 5), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(db.FoodItems);
    }

    [Fact]
    public async Task ConfirmAiFood_negative_nutrition_returns_validation_error()
    {
        await using var db = NewDb();
        await SeedActiveMembershipAsync(db);
        var service = CreateService(db, FakeAnalyzer.Success([]));

        var result = await service.ConfirmAiFoodAsync(
            new ConfirmAiFoodRequest("Com ga", -1, 10, 20, 5), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(db.FoodItems);
    }

    private sealed class FakeAnalyzer : IFoodImageAnalyzer
    {
        private readonly FoodImageAnalysisResult<IReadOnlyList<DetectedFood>> _result;
        private readonly FoodImageAnalysisResult<EstimatedFoodNutrition> _estimateResult;

        private FakeAnalyzer(
            FoodImageAnalysisResult<IReadOnlyList<DetectedFood>> result,
            FoodImageAnalysisResult<EstimatedFoodNutrition> estimateResult)
        {
            _result = result;
            _estimateResult = estimateResult;
        }

        public int CallCount { get; private set; }
        public int EstimateCallCount { get; private set; }

        public static FakeAnalyzer Success(IReadOnlyList<DetectedFood> foods)
            => new(
                FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Success(foods),
                DefaultEstimate());

        public static FakeAnalyzer Failure(string code, string message)
            => new(
                FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(code, message),
                DefaultEstimate());

        public static FakeAnalyzer EstimateSuccess(EstimatedFoodNutrition estimate)
            => new(
                FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Success([]),
                FoodImageAnalysisResult<EstimatedFoodNutrition>.Success(estimate));

        public static FakeAnalyzer EstimateFailure(string code, string message)
            => new(
                FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Success([]),
                FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(code, message));

        private static FoodImageAnalysisResult<EstimatedFoodNutrition> DefaultEstimate()
            => FoodImageAnalysisResult<EstimatedFoodNutrition>.Success(
                new EstimatedFoodNutrition("Test", 1, 100, 10, 10, 2));

        public Task<FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>> DetectFoodsAsync(
            byte[] imageBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }

        public Task<FoodImageAnalysisResult<EstimatedFoodNutrition>> EstimateNutritionAsync(
            string foodName,
            CancellationToken cancellationToken)
        {
            EstimateCallCount++;
            return Task.FromResult(_estimateResult);
        }
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
