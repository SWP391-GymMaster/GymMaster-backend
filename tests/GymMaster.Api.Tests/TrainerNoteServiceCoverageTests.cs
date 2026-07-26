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

// Bo sung coverage TrainerNoteService: GetByMember (moi role), Update, Delete + nhanh loi Create.
public class TrainerNoteServiceCoverageTests
{
    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"notecov-{Guid.NewGuid()}").Options);

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken ct)
            => Task.CompletedTask;
    }

    private static TrainerNoteService NewService(GymMasterDbContext db) => new(db, new NoopAudit());

    private static ClaimsPrincipal Principal(long userId, string role)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private sealed record Fixture(
        long MemberId, long MemberUserId,
        long TrainerId, long TrainerUserId,
        long OtherTrainerId, long OtherTrainerUserId);

    // 1 member + 2 PT; chi PT#1 duoc phan cong.
    private static async Task<Fixture> SeedAsync(GymMasterDbContext db)
    {
        var memberRole = new Role { Id = 4, Name = RoleNames.Member };
        var ptRole = new Role { Id = 3, Name = RoleNames.Pt };
        db.Roles.AddRange(memberRole, ptRole);

        var memberUser = new User { Email = "hv@gym.test", FullName = "Hoi Vien", PasswordHash = "x", Status = UserStatuses.Active };
        var ptUser = new User { Email = "pt1@gym.test", FullName = "PT Mot", PasswordHash = "x", Status = UserStatuses.Active };
        var otherPtUser = new User { Email = "pt2@gym.test", FullName = "PT Hai", PasswordHash = "x", Status = UserStatuses.Active };
        memberUser.UserRoles.Add(new UserRole { User = memberUser, Role = memberRole });
        ptUser.UserRoles.Add(new UserRole { User = ptUser, Role = ptRole });
        otherPtUser.UserRoles.Add(new UserRole { User = otherPtUser, Role = ptRole });
        db.Users.AddRange(memberUser, ptUser, otherPtUser);

        var member = new MemberProfile { User = memberUser };
        var trainer = new TrainerProfile { User = ptUser, Specialty = "Gym" };
        var otherTrainer = new TrainerProfile { User = otherPtUser, Specialty = "Yoga" };
        db.MemberProfiles.Add(member);
        db.TrainerProfiles.AddRange(trainer, otherTrainer);
        await db.SaveChangesAsync();

        db.TrainerAssignments.Add(new TrainerAssignment
        {
            MemberId = member.Id, TrainerId = trainer.Id, Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today(), CreatedByUserId = ptUser.Id, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return new Fixture(member.Id, memberUser.Id, trainer.Id, ptUser.Id, otherTrainer.Id, otherPtUser.Id);
    }

    private static async Task<long> SeedNoteAsync(GymMasterDbContext db, Fixture f, string content = "Buoi tap tot")
    {
        var result = await NewService(db).CreateAsync(
            f.MemberId, new CreateTrainerNoteRequest(content), Principal(f.TrainerUserId, RoleNames.Pt), default);
        return result.Value!.Id;
    }

    // ---------- CreateAsync: nhanh loi ----------

    [Fact]
    public async Task Create_empty_content_validation_error()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).CreateAsync(
            f.MemberId, new CreateTrainerNoteRequest("   "), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Create_unauthorized_when_no_actor()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).CreateAsync(
            f.MemberId, new CreateTrainerNoteRequest("x"), Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task Create_no_trainer_profile()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).CreateAsync(
            f.MemberId, new CreateTrainerNoteRequest("x"), Principal(9999, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Create_member_not_found()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).CreateAsync(
            999999, new CreateTrainerNoteRequest("x"), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Create_succeeds_for_assigned()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).CreateAsync(
            f.MemberId, new CreateTrainerNoteRequest("  Ghi chu  "), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Ghi chu", result.Value!.Content);
    }

    // ---------- GetByMemberAsync ----------

    [Fact]
    public async Task GetByMember_member_sees_own_notes()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        await SeedNoteAsync(db, f);

        var result = await NewService(db).GetByMemberAsync(f.MemberId, Principal(f.MemberUserId, RoleNames.Member), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetByMember_other_member_forbidden()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).GetByMemberAsync(f.MemberId, Principal(88888, RoleNames.Member), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task GetByMember_assigned_pt_sees_notes()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        await SeedNoteAsync(db, f);

        var result = await NewService(db).GetByMemberAsync(f.MemberId, Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetByMember_unassigned_pt_forbidden()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).GetByMemberAsync(f.MemberId, Principal(f.OtherTrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task GetByMember_admin_sees_all()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        await SeedNoteAsync(db, f);

        var result = await NewService(db).GetByMemberAsync(f.MemberId, Principal(1, RoleNames.Admin), default);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetByMember_not_found()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).GetByMemberAsync(999999, Principal(1, RoleNames.Admin), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task Update_by_owner_pt_succeeds()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).UpdateAsync(
            noteId, new UpdateTrainerNoteRequest("Noi dung moi"), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Noi dung moi", result.Value!.Content);
    }

    [Fact]
    public async Task Update_empty_content_validation_error()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).UpdateAsync(
            noteId, new UpdateTrainerNoteRequest(" "), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Update_no_trainer_profile()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).UpdateAsync(
            noteId, new UpdateTrainerNoteRequest("x"), Principal(9999, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Update_note_not_found()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).UpdateAsync(
            999999, new UpdateTrainerNoteRequest("x"), Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Update_by_other_pt_forbidden()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).UpdateAsync(
            noteId, new UpdateTrainerNoteRequest("x"), Principal(f.OtherTrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task Delete_by_owner_pt_succeeds()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).DeleteAsync(noteId, Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.True(result.Succeeded);
        Assert.False(await db.TrainerNotes.AnyAsync());
    }

    [Fact]
    public async Task Delete_no_trainer_profile()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).DeleteAsync(noteId, Principal(9999, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Delete_note_not_found()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);

        var result = await NewService(db).DeleteAsync(999999, Principal(f.TrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Delete_by_other_pt_forbidden()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var noteId = await SeedNoteAsync(db, f);

        var result = await NewService(db).DeleteAsync(noteId, Principal(f.OtherTrainerUserId, RoleNames.Pt), default);

        Assert.False(result.Succeeded);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }
}
