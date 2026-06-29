using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// FR-PKG-03/04: chi phan cong PT khi hoi vien co goi tap active ho tro PT.
public class AssignmentServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal Admin(long userId = 99)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Admin),
            },
            "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static AssignmentService NewService(GymMasterDbContext db)
        => new(db, new NoopAudit());

    // Seed 1 member + 1 trainer. Tuy chon them goi tap active (co/khong ho tro PT).
    private static void Seed(GymMasterDbContext db, bool? withActiveMembershipSupportsPt)
    {
        db.Users.Add(new User { Id = 10, FullName = "Member A", IsDeleted = false });
        db.Users.Add(new User { Id = 20, FullName = "Trainer B", IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.TrainerProfiles.Add(new TrainerProfile { Id = 2, UserId = 20, IsDeleted = false });

        if (withActiveMembershipSupportsPt is bool supportsPt)
        {
            var today = AppClock.Today();
            var pkg = new MembershipPackage
            {
                Id = 1,
                Name = supportsPt ? "Goi PT" : "Goi thuong",
                DurationDays = 30,
                Price = 500_000,
                IsActive = true,
                SupportsPT = supportsPt,
            };
            db.MembershipPackages.Add(pkg);
            db.Memberships.Add(new Membership
            {
                Id = 1,
                MemberId = 1,
                PackageId = 1,
                Package = pkg,
                StartDate = today,
                EndDate = today.AddDays(30),
                Status = MembershipStatus.Active,
                CreatedByUserId = 99,
            });
        }
    }

    [Fact] // Member khong co goi ho tro PT -> chan voi PACKAGE_PT_REQUIRED
    public async Task Assign_blocks_member_without_pt_supporting_package()
    {
        using var db = NewDb();
        Seed(db, withActiveMembershipSupportsPt: false);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(
            new AssignTrainerRequest(1, 2), Admin(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PACKAGE_PT_REQUIRED", result.ErrorCode);
        Assert.Equal(0, await db.TrainerAssignments.CountAsync());
    }

    [Fact] // Member khong co membership active nao -> cung bi chan
    public async Task Assign_blocks_member_without_any_active_membership()
    {
        using var db = NewDb();
        Seed(db, withActiveMembershipSupportsPt: null);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(
            new AssignTrainerRequest(1, 2), Admin(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PACKAGE_PT_REQUIRED", result.ErrorCode);
    }

    [Fact] // Member co goi active ho tro PT -> phan cong thanh cong
    public async Task Assign_succeeds_when_member_has_active_pt_supporting_package()
    {
        using var db = NewDb();
        Seed(db, withActiveMembershipSupportsPt: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(
            new AssignTrainerRequest(1, 2), Admin(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await db.TrainerAssignments.CountAsync(a => a.Status == AssignmentStatuses.Active));
    }
}
