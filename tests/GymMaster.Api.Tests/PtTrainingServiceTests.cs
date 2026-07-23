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

// Spec 005 — WorkoutPlanService + TrainerNoteService.
// Hai cho rui ro nhat: tra/tao exercise_catalog theo TEN (D-506) va UpdateAsync
// THAY TOAN BO danh sach bai tap (D-507). Cong voi cua quyen: PT chi thao tac
// tren member duoc phan cong, va chi sua/xoa ban ghi cua CHINH MINH.
public class PtTrainingServiceTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"pt-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static ClaimsPrincipal PtPrincipal(long userId)
        => new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Pt)
            }, "test"));

    private sealed record Fixture(
        GymMasterDbContext Db,
        MemberProfile Member,
        TrainerProfile Trainer,
        TrainerProfile OtherTrainer);

    // Mot member, hai PT — chi PT thu nhat duoc phan cong.
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
            MemberId = member.Id,
            TrainerId = trainer.Id,
            Status = AssignmentStatuses.Active,
            StartDate = AppClock.Today(),
            CreatedByUserId = ptUser.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return new Fixture(db, member, trainer, otherTrainer);
    }

    private static CreateWorkoutPlanRequest PlanWith(params string[] exerciseNames)
        => new(
            "Giao an tang co",
            "Tang co ban than",
            null,
            null,
            exerciseNames.Select((name, index) =>
                new WorkoutExerciseRequest(name, 3, "10", null, (short)index)).ToList());

    // ================= B-06: WorkoutPlanService =================

    [Fact]
    public async Task Create_plan_adds_unknown_exercises_into_the_catalog()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        Assert.Equal(0, await db.ExerciseCatalogs.CountAsync());

        var result = await service.CreateAsync(
            f.Member.Id, PlanWith("Bench Press", "Squat"), PtPrincipal(f.Trainer.UserId), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, await db.ExerciseCatalogs.CountAsync());
    }

    [Fact]
    public async Task Create_plan_reuses_an_existing_catalog_entry_for_the_same_name()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        await service.CreateAsync(f.Member.Id, PlanWith("Bench Press"), PtPrincipal(f.Trainer.UserId), default);
        await service.CreateAsync(f.Member.Id, PlanWith("Bench Press"), PtPrincipal(f.Trainer.UserId), default);

        // Cung mot bai tap -> KHONG duoc sinh hai dong catalog.
        Assert.Equal(1, await db.ExerciseCatalogs.CountAsync());
    }

    [Fact]
    public async Task Create_plan_refuses_the_same_exercise_twice_ignoring_case()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        // FindDuplicateExerciseName chay trong C# (khong phu thuoc collation DB) nen
        // kiem chung duoc bang InMemory. Service TU CHOI thay vi tu gop hai dong lai —
        // PT phai sua giao an cho ro rang.
        var result = await service.CreateAsync(
            f.Member.Id, PlanWith("Squat", "squat"), PtPrincipal(f.Trainer.UserId), default);

        Assert.False(result.Succeeded);
        Assert.Equal("DUPLICATE_EXERCISE", result.ErrorCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(0, await db.WorkoutPlans.CountAsync());
    }

    [Fact]
    public async Task Create_plan_numbers_sort_order_from_one_not_from_the_client_index()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        var created = await service.CreateAsync(
            f.Member.Id, PlanWith("Bench Press", "Squat"), PtPrincipal(f.Trainer.UserId), default);

        // Client gui OrderIndex 0-based; SortOrder trong DB phai la 1-based, neu khong
        // bai dau (0) roi vao fallback i+1=1 va trung voi bai thu hai -> vi pham UNIQUE.
        var orders = await db.WorkoutExercises
            .Where(item => item.WorkoutPlanId == created.Value!.Id)
            .Select(item => item.SortOrder)
            .OrderBy(item => item)
            .ToListAsync();

        Assert.Equal(new short[] { 1, 2 }, orders);
    }

    // ⚠️ KHOANG TRONG KIEM THU DA BIET — khong sua duoc bang EF InMemory.
    //
    // Khi ten bai tap DA nam trong DB voi kieu chu khac (vd DB co "Bench Press",
    // request gui "bench press"), viec khop nhau phu thuoc COLLATION cua SQL Server:
    //     .Where(e => names.Contains(e.Name))   -> dich thanh SQL "WHERE Name IN (...)"
    // Tren SQL Server (collation CI) thi khop; tren EF InMemory so sanh ordinal nen
    // KHONG khop -> se tao dong catalog trung.
    //
    // Nghia la nhanh nay chi dung tren DB that. Muon phu tu dong thi phai chay
    // integration test voi SQL Server that (vd Testcontainers) — xem BACKLOG.
    [Fact]
    public async Task Catalog_case_insensitive_match_against_stored_rows_needs_a_real_database()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        await service.CreateAsync(f.Member.Id, PlanWith("Bench Press"), PtPrincipal(f.Trainer.UserId), default);
        await service.CreateAsync(f.Member.Id, PlanWith("bench press"), PtPrincipal(f.Trainer.UserId), default);

        var count = await db.ExerciseCatalogs.CountAsync();

        // Chot lai hanh vi THUC TE cua InMemory de neu ai do doi cach tra cuu
        // (vd chuyen sang so sanh trong C#) thi test nay do va phai xem lai ghi chu tren.
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Create_plan_with_no_exercise_is_refused()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        var result = await service.CreateAsync(
            f.Member.Id, PlanWith(), PtPrincipal(f.Trainer.UserId), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact]
    public async Task Pt_cannot_create_a_plan_for_a_member_they_are_not_assigned_to()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());

        var result = await service.CreateAsync(
            f.Member.Id, PlanWith("Squat"), PtPrincipal(f.OtherTrainer.UserId), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Update_replaces_the_whole_exercise_list()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, PlanWith("Bench Press", "Squat", "Deadlift"), PtPrincipal(f.Trainer.UserId), default);
        var planId = created.Value!.Id;

        var updated = await service.UpdateAsync(
            planId,
            new UpdateWorkoutPlanRequest(null, null, null, null, null,
                new[] { new WorkoutExerciseRequest("Plank", 3, "60s", null, 0) }),
            PtPrincipal(f.Trainer.UserId),
            default);

        Assert.True(updated.Succeeded);
        // D-507: thay TOAN BO, khong phai merge -> chi con 1 bai.
        Assert.Single(updated.Value!.Exercises);
        Assert.Equal("Plank", updated.Value.Exercises[0].Name);
        Assert.Equal(1, await db.WorkoutExercises.CountAsync(item => item.WorkoutPlanId == planId));
    }

    [Fact]
    public async Task Update_without_exercises_keeps_the_existing_list()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, PlanWith("Bench Press", "Squat"), PtPrincipal(f.Trainer.UserId), default);

        var updated = await service.UpdateAsync(
            created.Value!.Id,
            new UpdateWorkoutPlanRequest("Ten moi", null, null, null, null, null),
            PtPrincipal(f.Trainer.UserId),
            default);

        Assert.True(updated.Succeeded);
        Assert.Equal(2, updated.Value!.Exercises.Count);
    }

    [Fact]
    public async Task Only_the_owning_pt_can_update_or_delete_a_plan()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, PlanWith("Squat"), PtPrincipal(f.Trainer.UserId), default);

        var update = await service.UpdateAsync(
            created.Value!.Id,
            new UpdateWorkoutPlanRequest("Doi ten", null, null, null, null, null),
            PtPrincipal(f.OtherTrainer.UserId), default);
        var delete = await service.DeleteAsync(created.Value.Id, PtPrincipal(f.OtherTrainer.UserId), default);

        Assert.Equal(StatusCodes.Status403Forbidden, update.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_plan_also_removes_its_exercises()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new WorkoutPlanService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, PlanWith("Bench Press", "Squat"), PtPrincipal(f.Trainer.UserId), default);
        var planId = created.Value!.Id;

        var deleted = await service.DeleteAsync(planId, PtPrincipal(f.Trainer.UserId), default);

        Assert.True(deleted.Succeeded);
        Assert.Equal(StatusCodes.Status204NoContent, deleted.StatusCode);
        Assert.Equal(0, await db.WorkoutPlans.CountAsync(item => item.Id == planId));
        Assert.Equal(0, await db.WorkoutExercises.CountAsync(item => item.WorkoutPlanId == planId));
    }

    // ================= B-07: TrainerNoteService =================

    [Fact]
    public async Task Pt_can_write_a_note_for_an_assigned_member()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new TrainerNoteService(db, new NoopAudit());

        var result = await service.CreateAsync(
            f.Member.Id, new CreateTrainerNoteRequest("Buoi tap tot"), PtPrincipal(f.Trainer.UserId), default);

        Assert.True(result.Succeeded);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(AppClock.Today(), result.Value!.NoteDate);
    }

    [Fact]
    public async Task Pt_cannot_write_a_note_for_a_member_they_are_not_assigned_to()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new TrainerNoteService(db, new NoopAudit());

        var result = await service.CreateAsync(
            f.Member.Id, new CreateTrainerNoteRequest("Ghi trom"), PtPrincipal(f.OtherTrainer.UserId), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Note_content_cannot_be_empty()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new TrainerNoteService(db, new NoopAudit());

        var result = await service.CreateAsync(
            f.Member.Id, new CreateTrainerNoteRequest("   "), PtPrincipal(f.Trainer.UserId), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Only_the_author_can_edit_a_note()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new TrainerNoteService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, new CreateTrainerNoteRequest("Ghi chu goc"), PtPrincipal(f.Trainer.UserId), default);

        var byOther = await service.UpdateAsync(
            created.Value!.Id, new UpdateTrainerNoteRequest("Sua trom"), PtPrincipal(f.OtherTrainer.UserId), default);

        Assert.Equal(StatusCodes.Status403Forbidden, byOther.StatusCode);

        var byOwner = await service.UpdateAsync(
            created.Value.Id, new UpdateTrainerNoteRequest("Da sua"), PtPrincipal(f.Trainer.UserId), default);

        Assert.True(byOwner.Succeeded);
        Assert.Equal("Da sua", byOwner.Value!.Content);
    }

    [Fact]
    public async Task Only_the_author_can_delete_a_note()
    {
        using var db = NewDb();
        var f = await SeedAsync(db);
        var service = new TrainerNoteService(db, new NoopAudit());
        var created = await service.CreateAsync(
            f.Member.Id, new CreateTrainerNoteRequest("Ghi chu"), PtPrincipal(f.Trainer.UserId), default);

        var byOther = await service.DeleteAsync(created.Value!.Id, PtPrincipal(f.OtherTrainer.UserId), default);
        Assert.Equal(StatusCodes.Status403Forbidden, byOther.StatusCode);

        var byOwner = await service.DeleteAsync(created.Value.Id, PtPrincipal(f.Trainer.UserId), default);
        Assert.True(byOwner.Succeeded);
        Assert.Equal(0, await db.TrainerNotes.CountAsync());
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
