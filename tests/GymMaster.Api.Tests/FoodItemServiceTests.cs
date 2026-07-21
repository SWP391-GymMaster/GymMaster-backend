using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Nutrition;

namespace GymMaster.Api.Tests;

// Test spec 007 - them mon an: Find-or-Create (trung ten thi tra mon co san, khong crash).
public class FoodItemServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
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

    [Fact] // Them mon moi -> tao moi
    public async Task AddFood_new_name_creates()
    {
        using var db = NewDb();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.AddAsync(
            new CreateFoodItemRequest("Chuoi", "qua", 89, 1.1m, 23m, 0.3m), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Chuoi", result.Value!.Name);
        Assert.Equal(1, await db.FoodItems.CountAsync());
    }

    [Fact] // Find-or-Create: trung ten -> tra mon co san, khong loi, khong tao dong moi
    public async Task AddFood_duplicate_name_returns_existing()
    {
        using var db = NewDb();
        db.FoodItems.Add(new FoodItem { Id = 5, Name = "Tao", Unit = "qua", CaloriesPerUnit = 52, IsActive = true });
        await db.SaveChangesAsync();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.AddAsync(
            new CreateFoodItemRequest("Tao", "qua", 52, null, null, null), Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Value!.Id);               // tra mon co san
        Assert.Equal(1, await db.FoodItems.CountAsync()); // khong tao dong moi
    }

    [Fact]
    public async Task Search_staff_gets_full_catalog_and_invalid_paging_is_normalized()
    {
        await using var db = NewDb();
        for (var i = 1; i <= 25; i++)
        {
            db.FoodItems.Add(new FoodItem
            {
                Id = i,
                Name = $"Food {i:D2}",
                Unit = "100g",
                CaloriesPerUnit = i,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(null, page: 0, pageSize: 999, Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(25, result.Value.Total);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Equal(20, result.Value.Items.Count);
    }

    [Fact]
    public async Task Search_member_without_active_package_is_limited_to_first_twenty_foods()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 10, Email = "member@test.local", FullName = "Member Test" });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10 });
        for (var i = 1; i <= 25; i++)
        {
            db.FoodItems.Add(new FoodItem
            {
                Id = i,
                Name = $"Food {i:D2}",
                Unit = "100g",
                CaloriesPerUnit = i,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(null, page: 1, pageSize: 100, Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(20, result.Value!.Total);
        Assert.Equal(20, result.Value.Items.Count);
        Assert.DoesNotContain(result.Value.Items, item => item.Name == "Food 21");
    }

    [Fact]
    public async Task Search_member_with_active_package_gets_full_catalog()
    {
        await using var db = NewDb();
        db.Users.Add(new User { Id = 10, Email = "member@test.local", FullName = "Member Test" });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10 });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1,
            Name = "Gold",
            DurationDays = 30,
            Price = 100_000
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
        for (var i = 1; i <= 25; i++)
        {
            db.FoodItems.Add(new FoodItem
            {
                Id = i,
                Name = $"Food {i:D2}",
                Unit = "100g",
                CaloriesPerUnit = i,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(null, page: 1, pageSize: 100, Member(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(25, result.Value!.Total);
        Assert.Equal(25, result.Value.Items.Count);
    }

    [Theory]
    [InlineData("", "100g", 100)]
    [InlineData("Rice", "", 100)]
    [InlineData("Rice", "100g", -1)]
    public async Task AddFood_invalid_input_returns_validation_error(string name, string unit, decimal calories)
    {
        await using var db = NewDb();
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.AddAsync(
            new CreateFoodItemRequest(name, unit, calories, 1, 1, 1), Member(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(db.FoodItems);
    }
}
