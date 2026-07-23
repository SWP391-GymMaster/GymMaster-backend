using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Nutrition;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Spec 007 AC-07 — tier mien phi (BIZ-13).
// Member CHUA co goi tap active chi tra duoc 20 mon dau (A→Z); gioi han ap
// TRUOC khi loc tu khoa, nen go dung ten mon nam ngoai 20 mon van khong thay.
// Day la rang buoc THUONG MAI, khong phai bao mat -> tra danh sach rong chu khong 403.
public class FoodItemTierTests
{
    private const int FreeTierSize = 20;

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"tier-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "test"));

    // 30 mon ten "Mon A01".."Mon A30" -> 20 mon dau la A01..A20.
    private static async Task SeedFoodsAsync(GymMasterDbContext db, int count = 30)
    {
        for (var i = 1; i <= count; i++)
        {
            db.FoodItems.Add(new FoodItem
            {
                Name = $"Mon A{i:D2}",
                Unit = "phan",
                CaloriesPerUnit = 100 + i,
                IsActive = true,
                ServingSize = 100,
                Source = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    // Tao member kem/khong kem goi tap dang hieu luc.
    private static async Task<User> SeedMemberAsync(GymMasterDbContext db, bool withActiveMembership)
    {
        var role = new Role { Id = 4, Name = RoleNames.Member };
        var user = new User { Email = "hv@gym.test", FullName = "Hoi Vien", PasswordHash = "x", Status = UserStatuses.Active };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Roles.Add(role);
        db.Users.Add(user);

        var profile = new MemberProfile { User = user };
        db.MemberProfiles.Add(profile);
        await db.SaveChangesAsync();

        if (withActiveMembership)
        {
            var package = new MembershipPackage
            {
                Name = "Goi thang", DurationDays = 30, Price = 500_000m, IsActive = true
            };
            db.MembershipPackages.Add(package);
            await db.SaveChangesAsync();

            var today = AppClock.Today();
            db.Memberships.Add(new Membership
            {
                MemberId = profile.Id,
                PackageId = package.Id,
                StartDate = today,
                EndDate = today.AddDays(30),
                Status = MembershipStatus.Active,
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return user;
    }

    [Fact]
    public async Task Member_without_an_active_package_sees_only_the_first_twenty_items()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        var user = await SeedMemberAsync(db, withActiveMembership: false);
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(
            null, 1, 100, Principal(user.Id, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Equal(FreeTierSize, result.Value!.Items.Count);
    }

    [Fact]
    public async Task Member_with_an_active_package_sees_the_whole_catalog()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        var user = await SeedMemberAsync(db, withActiveMembership: true);
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(
            null, 1, 100, Principal(user.Id, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Equal(30, result.Value!.Items.Count);
    }

    [Fact]
    public async Task Free_tier_universe_is_narrowed_before_any_further_filtering()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        var user = await SeedMemberAsync(db, withActiveMembership: false);
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(
            null, 1, 100, Principal(user.Id, RoleNames.Member), default);

        var names = result.Value!.Items.Select(item => item.Name).ToList();

        // Tap FREE la 20 mon DAU theo A→Z: phai co A01, khong duoc co A21 tro di.
        // Neu ai do doi thu tu (loc tu khoa truoc roi moi cat 20) thi tier mat tac dung.
        Assert.Contains("Mon A01", names);
        Assert.Contains("Mon A20", names);
        Assert.DoesNotContain("Mon A21", names);
        Assert.DoesNotContain("Mon A30", names);
    }

    [Fact]
    public async Task Free_tier_returns_an_empty_list_instead_of_forbidding_access()
    {
        using var db = NewDb();
        // Kho rong -> member free van duoc phep goi, chi la khong co gi de tra.
        var user = await SeedMemberAsync(db, withActiveMembership: false);
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(
            null, 1, 100, Principal(user.Id, RoleNames.Member), default);

        // Rang buoc THUONG MAI: khong duoc tra 403 (do la ngu nghia "thieu quyen").
        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Items);
    }

    // ⚠️ KHOANG TRONG KIEM THU DA BIET — khong sua duoc bang EF InMemory.
    //
    // Nhanh LOC TU KHOA dung EF.Functions.Collate(item.Name, "Latin1_General_100_CI_AI")
    // de tim khong phan biet DAU + hoa/thuong ("com" -> "Cơm"). Ham nay chi dich duoc
    // sang SQL Server; tren InMemory no nem InvalidOperationException.
    //
    // Hau qua: KHONG the tu dong kiem chung bang InMemory rang go dung ten mon thu 21+
    // van khong tim thay (dung tier). Can integration test voi SQL Server that.
    [Fact]
    public async Task Keyword_search_needs_a_real_sql_server_because_of_collate()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        var user = await SeedMemberAsync(db, withActiveMembership: false);
        var service = new FoodItemService(db, new NoopAudit());

        // Chot lai gioi han cua moi truong test, de ai do doi cach tim kiem
        // (vd bo Collate) thi test nay do va phai xem lai ghi chu tren.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync("Mon A25", 1, 100, Principal(user.Id, RoleNames.Member), default));
    }

    [Fact]
    public async Task Staff_is_never_limited_by_the_free_tier()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        await SeedMemberAsync(db, withActiveMembership: false);
        var service = new FoodItemService(db, new NoopAudit());

        var result = await service.SearchAsync(
            null, 1, 100, Principal(999, RoleNames.Staff), default);

        Assert.Equal(30, result.Value!.Items.Count);
    }

    [Fact]
    public async Task Expired_package_falls_back_to_the_free_tier()
    {
        using var db = NewDb();
        await SeedFoodsAsync(db);
        var user = await SeedMemberAsync(db, withActiveMembership: true);

        // Day goi ve qua khu -> khong con "hieu luc" theo BIZ-04.
        var membership = await db.Memberships.FirstAsync();
        membership.EndDate = AppClock.Today().AddDays(-1);
        await db.SaveChangesAsync();

        var service = new FoodItemService(db, new NoopAudit());
        var result = await service.SearchAsync(
            null, 1, 100, Principal(user.Id, RoleNames.Member), default);

        Assert.Equal(FreeTierSize, result.Value!.Items.Count);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
