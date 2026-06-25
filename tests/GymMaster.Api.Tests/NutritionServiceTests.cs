using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
}
