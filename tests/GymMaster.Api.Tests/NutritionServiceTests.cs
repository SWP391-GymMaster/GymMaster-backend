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
}
