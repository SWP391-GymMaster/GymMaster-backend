using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Features.Training;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymMaster.Api.Tests;

// Bo sung coverage AssignmentService: candidate list, assigned members, cac nhanh loi cua Assign.
public class AssignmentServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-asgcov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static AssignmentService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static ClaimsPrincipal Admin(long userId = 99)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Admin),
        }, "test"));

    private static ClaimsPrincipal Pt(long userId)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Pt),
        }, "test"));

    private static ClaimsPrincipal Anonymous()
        => new(new ClaimsIdentity());

    // Seed member(id=1,user=10) + trainer(id=2,user=20). Tuy chon goi active ho tro PT.
    private static void Seed(GymMasterDbContext db, bool withPtPackage)
    {
        db.Users.Add(new User { Id = 10, FullName = "Member A", Email = "mem@gym.local", Phone = "0900000001", Status = UserStatuses.Active, IsDeleted = false });
        db.Users.Add(new User { Id = 20, FullName = "Trainer B", Email = "pt@gym.local", Status = UserStatuses.Active, IsDeleted = false });
        db.MemberProfiles.Add(new MemberProfile { Id = 1, UserId = 10, IsDeleted = false });
        db.TrainerProfiles.Add(new TrainerProfile { Id = 2, UserId = 20, Specialty = "Yoga", IsDeleted = false });

        if (withPtPackage)
        {
            var today = AppClock.Today();
            var pkg = new MembershipPackage { Id = 1, Name = "Goi PT", DurationDays = 30, Price = 500_000, IsActive = true, SupportsPT = true };
            db.MembershipPackages.Add(pkg);
            db.Memberships.Add(new Membership
            {
                Id = 1, MemberId = 1, PackageId = 1, Package = pkg,
                StartDate = today, EndDate = today.AddDays(30),
                Status = MembershipStatus.Active, CreatedByUserId = 99,
            });
        }
    }

    // ---------- AssignAsync: nhanh loi ----------

    [Fact]
    public async Task Assign_unauthorized_when_no_actor_id()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(new AssignTrainerRequest(1, 2), Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task Assign_member_not_found()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(new AssignTrainerRequest(999, 2), Admin(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Assign_trainer_not_found()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(new AssignTrainerRequest(1, 999), Admin(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact] // FR-PT-02: da co PT active -> dong cai cu, tao cai moi
    public async Task Assign_replaces_existing_active_assignment()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        // Them trainer thu 2 va assignment active cu cho member 1
        db.Users.Add(new User { Id = 30, FullName = "Trainer C", Email = "pt2@gym.local", Status = UserStatuses.Active, IsDeleted = false });
        db.TrainerProfiles.Add(new TrainerProfile { Id = 3, UserId = 30, IsDeleted = false });
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            Id = 100, MemberId = 1, TrainerId = 3, Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today().AddDays(-10), CreatedByUserId = 99,
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).AssignAsync(new AssignTrainerRequest(1, 2), Admin(), default);

        Assert.True(result.Succeeded);
        var old = await db.TrainerAssignments.FindAsync(100L);
        Assert.Equal(AssignmentStatuses.Ended, old!.Status);
        // Chi con 1 assignment active
        Assert.Equal(1, await db.TrainerAssignments.CountAsync(a => a.Status == AssignmentStatuses.Active));
    }

    // ---------- GetCandidateMembersAsync ----------

    [Fact] // Chi member co goi PT active moi la ung vien; chua duoc phan cong
    public async Task CandidateMembers_returns_pt_eligible_unassigned()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCandidateMembersAsync(null, includeAssigned: false, default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(1, result.Value.Items[0].Id);
    }

    [Fact] // Member khong co goi PT -> khong xuat hien trong ung vien
    public async Task CandidateMembers_excludes_member_without_pt_package()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: false);
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCandidateMembersAsync(null, includeAssigned: false, default);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value!.Total);
    }

    [Fact] // Query loc theo ten
    public async Task CandidateMembers_filters_by_query()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var match = await NewService(db).GetCandidateMembersAsync("Member", false, default);
        var noMatch = await NewService(db).GetCandidateMembersAsync("Khong-ton-tai", false, default);

        Assert.Equal(1, match.Value!.Total);
        Assert.Equal(0, noMatch.Value!.Total);
    }

    [Fact] // includeAssigned=true van tra ve du member da co PT
    public async Task CandidateMembers_include_assigned()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            Id = 100, MemberId = 1, TrainerId = 2, Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today(), CreatedByUserId = 99,
        });
        await db.SaveChangesAsync();

        var excluded = await NewService(db).GetCandidateMembersAsync(null, includeAssigned: false, default);
        var included = await NewService(db).GetCandidateMembersAsync(null, includeAssigned: true, default);

        Assert.Equal(0, excluded.Value!.Total);
        Assert.Equal(1, included.Value!.Total);
        Assert.Equal(2, included.Value.Items[0].CurrentTrainerId);
    }

    // ---------- GetCandidateTrainersAsync ----------

    [Fact]
    public async Task CandidateTrainers_returns_all_with_active_count()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            Id = 100, MemberId = 1, TrainerId = 2, Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today(), CreatedByUserId = 99,
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCandidateTrainersAsync(null, null, default);

        Assert.True(result.Succeeded);
        var trainer = Assert.Single(result.Value!.Items);
        Assert.Equal(2, trainer.Id);
        Assert.Equal(1, trainer.AssignedCount);
    }

    [Fact] // Loc theo specialty
    public async Task CandidateTrainers_filters_by_specialty()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var match = await NewService(db).GetCandidateTrainersAsync(null, "Yoga", default);
        var noMatch = await NewService(db).GetCandidateTrainersAsync(null, "Boxing", default);

        Assert.Equal(1, match.Value!.Total);
        Assert.Equal(0, noMatch.Value!.Total);
    }

    [Fact] // Loc theo query ten
    public async Task CandidateTrainers_filters_by_query()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCandidateTrainersAsync("Trainer", null, default);

        Assert.Equal(1, result.Value!.Total);
    }

    // ---------- GetAssignedMembersAsync ----------

    [Fact] // PT xem member duoc phan cong cho minh
    public async Task AssignedMembers_returns_active_for_trainer()
    {
        using var db = NewDb();
        Seed(db, withPtPackage: true);
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            Id = 100, MemberId = 1, TrainerId = 2, Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today(), CreatedByUserId = 99,
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetAssignedMembersAsync(Pt(20), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
        Assert.Equal(1, result.Value![0].Id);
    }

    [Fact]
    public async Task AssignedMembers_unauthorized_when_no_actor()
    {
        using var db = NewDb();
        var result = await NewService(db).GetAssignedMembersAsync(Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact] // User khong co ho so PT -> NOT_FOUND
    public async Task AssignedMembers_not_found_when_no_trainer_profile()
    {
        using var db = NewDb();
        var result = await NewService(db).GetAssignedMembersAsync(Pt(777), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }
}
