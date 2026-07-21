using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Common;
using GymMaster.API.Features.Nutrition;

namespace GymMaster.Api.Tests;

// Test phan dinh duong (007): mon trong meal-log tra kem macro = macro/don vi x khau phan.
public class NutritionServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Staff(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Staff),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal Member(long userId = 10)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Member),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task SeedMemberAsync(GymMasterDbContext db, long memberId = 1, long userId = 10)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"member{userId}@test.local",
            FullName = "Member Test",
            IsDeleted = false
        });
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = memberId,
            UserId = userId,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact] // FR-MEAL-01 / FR-CAL-01: mon trong meal-log tra calo + macro (gia tri/don vi x khau phan)
    public async Task MealLog_item_returns_calories_and_macros()
    {
        using var db = NewDb();
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.FoodItems.Add(new FoodItem
        {
            Id = 1,
            Name = "Uc ga",
            Unit = "100g",
            CaloriesPerUnit = 100,
            ProteinG = 10,
            CarbG = 20,
            FatG = 5,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new NutritionService(db, new NoopAudit());
        var today = AppClock.Today();

        await service.CreateMealLogAsync(
            new CreateMealLogRequest(1, today, (byte)MealType.Breakfast, new[] { new MealItemInput(1, 2) }),
            Staff(), default);

        var logs = await service.GetMealLogsAsync(1, today, Staff(), default);

        Assert.True(logs.Succeeded);
        var item = logs.Value!.Single().Items.Single();
        Assert.Equal(200m, item.Calories);  // 100 x 2
        Assert.Equal(20m, item.ProteinG);   // 10 x 2
        Assert.Equal(40m, item.CarbG);      // 20 x 2
        Assert.Equal(10m, item.FatG);       // 5 x 2
    }

    [Fact] // FR-CAL-01: chua dat target thi target/remaining macro null, consumed macro van tinh tu mon da an.
    public async Task Summary_without_target_returns_consumed_macros_and_null_targets()
    {
        using var db = NewDb();
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.FoodItems.Add(new FoodItem
        {
            Id = 1,
            Name = "Uc ga",
            Unit = "100g",
            CaloriesPerUnit = 100,
            ProteinG = 10,
            CarbG = 20,
            FatG = 5,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = new NutritionService(db, new NoopAudit());
        var today = AppClock.Today();

        await service.CreateMealLogAsync(
            new CreateMealLogRequest(1, today, (byte)MealType.Breakfast, new[] { new MealItemInput(1, 2) }),
            Staff(), default);

        var summary = await service.GetSummaryAsync(1, today, Staff(), default);

        Assert.True(summary.Succeeded);
        Assert.Equal(200m, summary.Value!.Consumed);
        Assert.Null(summary.Value.Target);
        Assert.Null(summary.Value.Remaining);
        Assert.Equal(20m, summary.Value.ConsumedProteinG);
        Assert.Equal(40m, summary.Value.ConsumedCarbG);
        Assert.Equal(10m, summary.Value.ConsumedFatG);
        Assert.Null(summary.Value.TargetProteinG);
        Assert.Null(summary.Value.TargetCarbG);
        Assert.Null(summary.Value.TargetFatG);
        Assert.Null(summary.Value.RemainingProteinG);
        Assert.Null(summary.Value.RemainingCarbG);
        Assert.Null(summary.Value.RemainingFatG);
    }

    [Fact] // FR-CAL-01: summary tra consumed, target va remaining cho calo + 3 macro.
    public async Task Summary_with_target_returns_consumed_target_and_remaining_macros()
    {
        using var db = NewDb();
        var today = AppClock.Today();
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.FoodItems.Add(new FoodItem
        {
            Id = 1,
            Name = "Uc ga",
            Unit = "100g",
            CaloriesPerUnit = 100,
            ProteinG = 10,
            CarbG = 20,
            FatG = 5,
            IsActive = true,
        });
        db.CalorieTargets.Add(new CalorieTarget
        {
            Id = 1,
            MemberId = 1,
            EffectiveDate = today,
            DailyCalories = 500,
            ProteinG = 80,
            CarbG = 200,
            FatG = 50,
        });
        await db.SaveChangesAsync();

        var service = new NutritionService(db, new NoopAudit());

        await service.CreateMealLogAsync(
            new CreateMealLogRequest(1, today, (byte)MealType.Breakfast, new[] { new MealItemInput(1, 2) }),
            Staff(), default);

        var summary = await service.GetSummaryAsync(1, today, Staff(), default);

        Assert.True(summary.Succeeded);
        Assert.Equal(200m, summary.Value!.Consumed);
        Assert.Equal(500m, summary.Value.Target);
        Assert.Equal(300m, summary.Value.Remaining);
        Assert.Equal(20m, summary.Value.ConsumedProteinG);
        Assert.Equal(40m, summary.Value.ConsumedCarbG);
        Assert.Equal(10m, summary.Value.ConsumedFatG);
        Assert.Equal(80m, summary.Value.TargetProteinG);
        Assert.Equal(200m, summary.Value.TargetCarbG);
        Assert.Equal(50m, summary.Value.TargetFatG);
        Assert.Equal(60m, summary.Value.RemainingProteinG);
        Assert.Equal(160m, summary.Value.RemainingCarbG);
        Assert.Equal(40m, summary.Value.RemainingFatG);
    }

    [Fact]
    public async Task GetTargetAsync_when_no_target_returns_not_found_with_no_target_code()
    {
        using var db = NewDb();
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        await db.SaveChangesAsync();

        var service = new NutritionService(db, new NoopAudit());
        var result = await service.GetTargetAsync(1, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NO_TARGET", result.ErrorCode);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetTargetAsync_when_has_target_returns_latest_valid_target()
    {
        using var db = NewDb();
        var today = AppClock.Today();
        db.Users.Add(new User { Id = 10, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });

        // Older target
        db.CalorieTargets.Add(new CalorieTarget
        {
            Id = 1,
            MemberId = 1,
            EffectiveDate = today.AddDays(-2),
            DailyCalories = 2000,
            ProteinG = 120,
            CarbG = 250,
            FatG = 70
        });

        // Latest valid target
        db.CalorieTargets.Add(new CalorieTarget
        {
            Id = 2,
            MemberId = 1,
            EffectiveDate = today.AddDays(-1),
            DailyCalories = 2500,
            ProteinG = 150,
            CarbG = 300,
            FatG = 80
        });

        // Future target (not valid yet)
        db.CalorieTargets.Add(new CalorieTarget
        {
            Id = 3,
            MemberId = 1,
            EffectiveDate = today.AddDays(1),
            DailyCalories = 3000,
            ProteinG = 180,
            CarbG = 350,
            FatG = 90
        });

        await db.SaveChangesAsync();

        var service = new NutritionService(db, new NoopAudit());
        var result = await service.GetTargetAsync(1, Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2500m, result.Value!.DailyCalories);
        Assert.Equal(150m, result.Value.ProteinG);
        Assert.Equal(300m, result.Value.CarbG);
        Assert.Equal(80m, result.Value.FatG);
    }

    [Fact]
    public async Task SetTargetAsync_creates_then_updates_same_effective_date()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());
        var date = AppClock.Today();

        var created = await service.SetTargetAsync(
            1,
            new SetCalorieTargetRequest(date, 2_000, 120, 250, 60),
            Staff(),
            default);
        var updated = await service.SetTargetAsync(
            1,
            new SetCalorieTargetRequest(date, 2_200, 140, 270, 70),
            Staff(),
            default);

        Assert.True(created.Succeeded);
        Assert.Equal(201, created.StatusCode);
        Assert.True(updated.Succeeded);
        Assert.Equal(200, updated.StatusCode);
        Assert.Equal(2_200m, updated.Value!.DailyCalories);
        Assert.Equal(140m, updated.Value.ProteinG);
        Assert.Equal(1, await db.CalorieTargets.CountAsync());
    }

    [Fact]
    public async Task SetTargetAsync_for_other_member_returns_forbidden()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db, memberId: 1, userId: 10);
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.SetTargetAsync(
            1,
            new SetCalorieTargetRequest(null, 2_000, 120, 250, 60),
            Member(userId: 11),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(db.CalorieTargets);
    }

    [Theory]
    [InlineData(0, 100, 100, 100)]
    [InlineData(2000, -1, 100, 100)]
    [InlineData(2000, 100, -1, 100)]
    [InlineData(2000, 100, 100, -1)]
    public async Task SetTargetAsync_invalid_nutrition_returns_unprocessable_entity(
        decimal calories,
        decimal protein,
        decimal carbs,
        decimal fat)
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.SetTargetAsync(
            1,
            new SetCalorieTargetRequest(null, calories, protein, carbs, fat),
            Staff(),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_TARGET", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task CreateMealLogAsync_groups_duplicate_foods_and_accumulates_existing_meal()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        db.FoodItems.Add(new FoodItem
        {
            Id = 1,
            Name = "Com ga",
            Unit = "phan",
            CaloriesPerUnit = 100,
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = new NutritionService(db, new NoopAudit());
        var date = AppClock.Today();

        var first = await service.CreateMealLogAsync(
            new CreateMealLogRequest(
                1,
                date,
                (byte)MealType.Lunch,
                new[] { new MealItemInput(1, 1), new MealItemInput(1, 2) }),
            Staff(),
            default);
        var second = await service.CreateMealLogAsync(
            new CreateMealLogRequest(
                1,
                date,
                (byte)MealType.Lunch,
                new[] { new MealItemInput(1, 1) }),
            Staff(),
            default);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, await db.MealLogs.CountAsync());
        Assert.Equal(1, await db.MealLogItems.CountAsync());
        var item = second.Value!.Items.Single();
        Assert.Equal(4m, item.Quantity);
        Assert.Equal(400m, item.Calories);
    }

    [Fact]
    public async Task CreateMealLogAsync_invalid_quantity_is_rejected_before_food_query()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.CreateMealLogAsync(
            new CreateMealLogRequest(
                1,
                AppClock.Today(),
                (byte)MealType.Breakfast,
                new[] { new MealItemInput(999, 0) }),
            Staff(),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_QUANTITY", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task CreateMealLogAsync_unknown_meal_type_returns_validation_error()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.CreateMealLogAsync(
            new CreateMealLogRequest(
                1,
                AppClock.Today(),
                255,
                new[] { new MealItemInput(999, 1) }),
            Staff(),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task CreateMealLogAsync_unknown_food_returns_not_found()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.CreateMealLogAsync(
            new CreateMealLogRequest(
                1,
                AppClock.Today(),
                (byte)MealType.Dinner,
                new[] { new MealItemInput(999, 1) }),
            Staff(),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal("FOOD_NOT_FOUND", result.ErrorCode);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetHistoryAsync_invalid_range_returns_validation_error()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var service = new NutritionService(db, new NoopAudit());
        var today = AppClock.Today();

        var result = await service.GetHistoryAsync(1, today, today.AddDays(-1), Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_inclusive_dates_consumed_calories_and_effective_target()
    {
        await using var db = NewDb();
        await SeedMemberAsync(db);
        var start = AppClock.Today().AddDays(-2);
        db.CalorieTargets.AddRange(
            new CalorieTarget { Id = 1, MemberId = 1, EffectiveDate = start, DailyCalories = 2_000 },
            new CalorieTarget { Id = 2, MemberId = 1, EffectiveDate = start.AddDays(2), DailyCalories = 2_500 });
        db.MealLogs.Add(new MealLog
        {
            Id = 1,
            MemberId = 1,
            LogDate = start.AddDays(1),
            MealType = MealType.Lunch,
            Items =
            [
                new MealLogItem { Id = 1, FoodItemId = 1, Quantity = 1, Calories = 600 }
            ]
        });
        await db.SaveChangesAsync();
        var service = new NutritionService(db, new NoopAudit());

        var result = await service.GetHistoryAsync(1, start, start.AddDays(2), Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(0m, result.Value[0].Consumed);
        Assert.Equal(2_000m, result.Value[0].Target);
        Assert.Equal(600m, result.Value[1].Consumed);
        Assert.Equal(1_400m, result.Value[1].Remaining);
        Assert.Equal(2_500m, result.Value[2].Target);
    }
}
