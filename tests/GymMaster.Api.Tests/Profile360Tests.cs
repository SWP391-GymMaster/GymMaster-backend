using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Nutrition;
using GymMaster.API.Features.Training;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Spec 006 AC-06 — quy tac suy currentMembership (FR-360-03, BIZ-17):
//   Active con han (EndDate lon nhat)  ->  PendingPayment moi nhat  ->  null
// KHONG duoc fallback dong bat ky: member chi con don Cancelled/Expired thi phai
// tra null, neu khong man hinh 360 se hien goi da chet nhu "goi hien tai".
public class Profile360Tests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"p360-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ProgressService NewService(GymMasterDbContext db)
        => new(db, new NoopAudit(), new NutritionService(db, new NoopAudit()));

    private static ClaimsPrincipal Staff(long userId = 99)
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Staff)
            }, "test"));

    private const long MemberId = 1;

    private static async Task SeedMemberAsync(GymMasterDbContext db)
    {
        db.Users.Add(new User { Id = 10, Email = "hv@gym.test", FullName = "Hoi Vien", PasswordHash = "x", IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = MemberId, UserId = 10, IsDeleted = false });
        db.MembershipPackages.Add(new MembershipPackage
        {
            Id = 1, Name = "Goi thang", DurationDays = 30, Price = 500_000m, IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddMembershipAsync(
        GymMasterDbContext db,
        MembershipStatus status,
        DateOnly endDate,
        long id,
        DateTime? createdAt = null)
    {
        db.Memberships.Add(new Membership
        {
            Id = id,
            MemberId = MemberId,
            PackageId = 1,
            StartDate = endDate.AddDays(-30),
            EndDate = endDate,
            Status = status,
            CreatedByUserId = 10,
            CreatedAt = createdAt ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Current_membership_is_null_when_only_cancelled_and_expired_orders_remain()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        var today = AppClock.Today();
        await AddMembershipAsync(db, MembershipStatus.Cancelled, today.AddDays(20), id: 1);
        await AddMembershipAsync(db, MembershipStatus.Expired, today.AddDays(-5), id: 2);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.CurrentMembership);
        // Lich su van phai day du — chi "goi hien tai" moi la null.
        Assert.Equal(2, result.Value.MembershipHistory.Count);
    }

    [Fact]
    public async Task Current_membership_is_null_when_the_member_has_no_membership_at_all()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.CurrentMembership);
        Assert.Empty(result.Value.MembershipHistory);
    }

    [Fact]
    public async Task Active_package_wins_over_a_pending_order()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        var today = AppClock.Today();
        await AddMembershipAsync(db, MembershipStatus.PendingPayment, today.AddDays(30), id: 1);
        await AddMembershipAsync(db, MembershipStatus.Active, today.AddDays(10), id: 2);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.Equal(2, result.Value!.CurrentMembership!.Id);
        Assert.Equal(nameof(MembershipStatus.Active), result.Value.CurrentMembership.Status);
    }

    [Fact]
    public async Task The_active_package_with_the_furthest_end_date_is_chosen()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        var today = AppClock.Today();
        await AddMembershipAsync(db, MembershipStatus.Active, today.AddDays(5), id: 1);
        await AddMembershipAsync(db, MembershipStatus.Active, today.AddDays(40), id: 2);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.Equal(2, result.Value!.CurrentMembership!.Id);
    }

    [Fact]
    public async Task A_pending_order_is_used_when_there_is_no_active_package()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        var today = AppClock.Today();
        await AddMembershipAsync(db, MembershipStatus.Expired, today.AddDays(-10), id: 1);
        await AddMembershipAsync(db, MembershipStatus.PendingPayment, today.AddDays(30), id: 2);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.Equal(2, result.Value!.CurrentMembership!.Id);
        Assert.Equal(nameof(MembershipStatus.PendingPayment), result.Value.CurrentMembership.Status);
    }

    [Fact]
    public async Task The_newest_pending_order_is_chosen_when_several_exist()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        var today = AppClock.Today();
        await AddMembershipAsync(db, MembershipStatus.PendingPayment, today.AddDays(30), id: 1,
            createdAt: DateTime.UtcNow.AddMinutes(-20));
        await AddMembershipAsync(db, MembershipStatus.PendingPayment, today.AddDays(30), id: 2,
            createdAt: DateTime.UtcNow.AddMinutes(-5));

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.Equal(2, result.Value!.CurrentMembership!.Id);
    }

    [Fact]
    public async Task An_active_package_past_its_end_date_is_expired_before_the_answer_is_built()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        // Active nhung da qua han -> lazy expire (GBL-10) phai chay TRUOC khi tra ket qua.
        await AddMembershipAsync(db, MembershipStatus.Active, AppClock.Today().AddDays(-1), id: 1);

        var result = await NewService(db).GetProfile360Async(MemberId, Staff(), default);

        Assert.Null(result.Value!.CurrentMembership);
        var stored = await db.Memberships.FirstAsync(item => item.Id == 1);
        Assert.Equal(MembershipStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task Profile_360_returns_404_for_an_unknown_member()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);

        var result = await NewService(db).GetProfile360Async(9999, Staff(), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }
}
