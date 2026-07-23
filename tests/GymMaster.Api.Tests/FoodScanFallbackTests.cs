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
using Xunit;

namespace GymMaster.Api.Tests;

// Spec 009 AC-06 / SAFE-12 — dich vu ngoai loi KHONG duoc chan luong nghiep vu chinh.
//
// FoodScanServiceTests da kiem "analyzer loi -> 502" o muc rieng le. Cai con thieu la
// bang chung DAU-CUOI: khi Gemini chet, nguoi dung VAN ghi duoc nhat ky an bang tay
// (spec 007) va so calo trong ngay VAN dung.
public class FoodScanFallbackTests
{
    private const long MemberId = 1;
    private const long MemberUserId = 10;

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"fallback-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Member()
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, MemberUserId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Member)
            }, "test"));

    private static IFormFile Image(string contentType = "image/jpeg", int size = 1024)
    {
        var bytes = new byte[size];
        return new FormFile(new MemoryStream(bytes), 0, size, "image", "bua-an.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static async Task SeedMemberWithActivePackageAsync(GymMasterDbContext db)
    {
        var role = new Role { Id = 4, Name = RoleNames.Member };
        var user = new User { Id = MemberUserId, Email = "hv@gym.test", FullName = "Hoi Vien", PasswordHash = "x" };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Roles.Add(role);
        db.Users.Add(user);
        db.MemberProfiles.Add(new MemberProfile { Id = MemberId, UserId = MemberUserId });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1, Name = "Goi thang", DurationDays = 30, Price = 500_000m, IsActive = true
        });
        db.Memberships.Add(new Membership
        {
            Id = 1,
            MemberId = MemberId,
            PackageId = 1,
            StartDate = AppClock.Today().AddDays(-1),
            EndDate = AppClock.Today().AddDays(29),
            Status = MembershipStatus.Active,
            CreatedByUserId = MemberUserId
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task When_gemini_is_down_the_manual_meal_journal_still_works_end_to_end()
    {
        await using var db = NewDb();
        await SeedMemberWithActivePackageAsync(db);

        var brokenAnalyzer = new AlwaysFailingAnalyzer();
        var scanService = new FoodScanService(
            db, brokenAnalyzer, new NoopAudit(), Options.Create(new GeminiOptions { MaxImageBytes = 5_000_000 }));
        var foodService = new FoodItemService(db, new NoopAudit());
        var nutritionService = new NutritionService(db, new NoopAudit());

        // 1) Quet anh that bai -> 502, KHONG tao FoodItem/MealLog nao.
        var scan = await scanService.ScanImageAsync(Image(), Member(), default);

        Assert.False(scan.Succeeded);
        Assert.Equal(StatusCodes.Status502BadGateway, scan.StatusCode);
        Assert.Equal(0, await db.FoodItems.CountAsync());
        Assert.Equal(0, await db.MealLogs.CountAsync());

        // 2) Nguoi dung chuyen sang nhap tay — day chinh la fallback cua spec 007.
        var food = await foodService.AddAsync(
            new CreateFoodItemRequest("Com tam", "phan", 600m, 25m, 80m, 15m), Member(), default);
        Assert.True(food.Succeeded);

        var meal = await nutritionService.CreateMealLogAsync(
            new CreateMealLogRequest(
                MemberId,
                AppClock.Today(),
                MealType: 2,
                new[] { new MealItemInput(food.Value!.Id, 2m) }),
            Member(),
            default);
        Assert.True(meal.Succeeded);

        // 3) So calo trong ngay van dung: 600 x 2 phan.
        var summary = await nutritionService.GetSummaryAsync(MemberId, AppClock.Today(), Member(), default);

        Assert.True(summary.Succeeded);
        Assert.Equal(1200m, summary.Value!.Consumed);
    }

    [Fact]
    public async Task A_failing_analyzer_never_writes_anything_to_the_database()
    {
        await using var db = NewDb();
        await SeedMemberWithActivePackageAsync(db);

        var brokenAnalyzer = new AlwaysFailingAnalyzer();
        var scanService = new FoodScanService(
            db, brokenAnalyzer, new NoopAudit(), Options.Create(new GeminiOptions { MaxImageBytes = 5_000_000 }));

        for (var i = 0; i < 3; i++)
        {
            await scanService.ScanImageAsync(Image(), Member(), default);
        }

        // SAFE-09: AI khong tu quyet. Loi lien tiep cung khong duoc de lai rac.
        Assert.Equal(3, brokenAnalyzer.CallCount);
        Assert.Equal(0, await db.FoodItems.CountAsync());
        Assert.Equal(0, await db.MealLogs.CountAsync());
        Assert.Equal(0, await db.MealLogItems.CountAsync());
    }

    [Fact]
    public async Task Confirming_a_draft_still_works_after_a_failed_scan()
    {
        await using var db = NewDb();
        await SeedMemberWithActivePackageAsync(db);

        var brokenAnalyzer = new AlwaysFailingAnalyzer();
        var scanService = new FoodScanService(
            db, brokenAnalyzer, new NoopAudit(), Options.Create(new GeminiOptions { MaxImageBytes = 5_000_000 }));

        await scanService.ScanImageAsync(Image(), Member(), default);

        // Quet hong khong lam hong duong xac nhan mon: nguoi dung tu go dinh duong.
        var confirmed = await scanService.ConfirmAiFoodAsync(
            new ConfirmAiFoodRequest("Pho bo", 450m, 20m, 60m, 10m), Member(), default);

        Assert.True(confirmed.Succeeded);
        var saved = await db.FoodItems.SingleAsync();
        Assert.Equal("Pho bo", saved.Name);
        Assert.Equal("AI", saved.Source);
    }

    // Mo phong Gemini timeout / loi mang: luon tra Failure.
    private sealed class AlwaysFailingAnalyzer : IFoodImageAnalyzer
    {
        public int CallCount { get; private set; }

        public Task<FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>> DetectFoodsAsync(
            byte[] imageBytes, string contentType, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(
                FoodImageAnalysisResult<IReadOnlyList<DetectedFood>>.Failure(
                    "GEMINI_TIMEOUT", "Gemini khong phan hoi trong thoi gian cho."));
        }

        public Task<FoodImageAnalysisResult<EstimatedFoodNutrition>> EstimateNutritionAsync(
            string foodName, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                FoodImageAnalysisResult<EstimatedFoodNutrition>.Failure(
                    "GEMINI_TIMEOUT", "Gemini khong phan hoi trong thoi gian cho."));
        }
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
