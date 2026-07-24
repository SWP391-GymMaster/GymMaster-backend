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

// Spec 006 FR-360-02 + AC-06: ma tran quyen truy cap va quy tac suy currentMembership
// (FR-360-03, BIZ-17):
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

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "test"));

    private static ClaimsPrincipal Staff(long userId = 99)
        => Principal(userId, RoleNames.Staff);

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

    private static async Task SeedTrainerAsync(
        GymMasterDbContext db,
        long userId,
        long trainerId,
        byte? assignmentStatus)
    {
        var user = new User
        {
            Id = userId,
            Email = $"pt{userId}@gym.test",
            FullName = $"PT {userId}",
            PasswordHash = "x",
            IsDeleted = false
        };
        var trainer = new TrainerProfile
        {
            Id = trainerId,
            UserId = userId,
            User = user,
            IsDeleted = false
        };

        db.Users.Add(user);
        db.TrainerProfiles.Add(trainer);

        if (assignmentStatus is not null)
        {
            db.TrainerAssignments.Add(new TrainerAssignment
            {
                Id = trainerId,
                MemberId = MemberId,
                TrainerId = trainerId,
                StartDate = AppClock.Today(),
                Status = assignmentStatus.Value,
                CreatedByUserId = 99
            });
        }

        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Staff)]
    public async Task Admin_and_staff_can_view_any_member_profile_360(string role)
    {
        using var db = NewDb();
        await SeedMemberAsync(db);

        var result = await NewService(db).GetProfile360Async(
            MemberId,
            Principal(userId: 99, role),
            default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Member_can_view_their_own_profile_360()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);

        var result = await NewService(db).GetProfile360Async(
            MemberId,
            Principal(userId: 10, RoleNames.Member),
            default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Member_cannot_view_another_members_profile_360()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);

        var result = await NewService(db).GetProfile360Async(
            MemberId,
            Principal(userId: 11, RoleNames.Member),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Assigned_pt_can_view_the_members_profile_360()
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        await SeedTrainerAsync(db, userId: 20, trainerId: 2, AssignmentStatuses.Active);

        var result = await NewService(db).GetProfile360Async(
            MemberId,
            Principal(userId: 20, RoleNames.Pt),
            default);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AssignmentStatuses.Ended)]
    public async Task Pt_without_an_active_assignment_cannot_view_the_members_profile_360(
        byte? assignmentStatus)
    {
        using var db = NewDb();
        await SeedMemberAsync(db);
        await SeedTrainerAsync(db, userId: 20, trainerId: 2, assignmentStatus);

        var result = await NewService(db).GetProfile360Async(
            MemberId,
            Principal(userId: 20, RoleNames.Pt),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
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
