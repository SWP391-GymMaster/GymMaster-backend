using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using GymMaster.API.Common;
using GymMaster.API.Features.Dashboard;

namespace GymMaster.API.Features.Training;
public sealed class WorkoutPlanService : IWorkoutPlanService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public WorkoutPlanService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-WP-01: PT tao WorkoutPlan cho member duoc phan cong.
    public async Task<ServiceResult<WorkoutPlanResponse>> CreateAsync(
        long memberId,
        CreateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Fail<WorkoutPlanResponse>("VALIDATION_ERROR", "Tieu de giao an khong duoc de trong.", StatusCodes.Status400BadRequest);
        }

        if (request.Exercises is null || request.Exercises.Count == 0)
        {
            return Fail<WorkoutPlanResponse>("EMPTY_PLAN", "Giao an phai co it nhat 1 bai tap.", StatusCodes.Status422UnprocessableEntity);
        }

        var trainerProfile = await GetTrainerProfileAsync(principal, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<WorkoutPlanResponse>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var memberProfile = await GetAssignedMemberAsync(trainerProfile.Id, memberId, cancellationToken);

        if (memberProfile is null)
        {
            return Fail<WorkoutPlanResponse>("FORBIDDEN", "PT chua duoc phan cong cho hoi vien nay.", StatusCodes.Status403Forbidden);
        }

        var exerciseMap = await ResolveExercisesAsync(request.Exercises, cancellationToken);

        var today = AppClock.Today();
        var now = DateTime.UtcNow;
        var plan = new WorkoutPlan
        {
            MemberId = memberId,
            TrainerId = trainerProfile.Id,
            Title = request.Title.Trim(),
            Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim(),
            StartDate = request.StartDate ?? today,
            EndDate = request.EndDate,
            Status = WorkoutPlanStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var i = 0; i < request.Exercises.Count; i++)
        {
            var ex = request.Exercises[i];
            var exerciseId = exerciseMap[ex.Name.Trim()];
            plan.Exercises.Add(new WorkoutExercise
            {
                ExerciseId = exerciseId,
                // SortOrder luon theo vi tri trong list (1-based). KHONG dung ex.OrderIndex:
                // client gui 0-based nen bai dau (0) roi vao fallback i+1=1, trung voi bai
                // thu 2 (OrderIndex=1) -> Violation UQ_workout_exercises_Plan_Order (2627) -> 500.
                SortOrder = (short)(i + 1),
                Sets = ex.Sets,
                Reps = ParseReps(ex.Reps),
                Note = string.IsNullOrWhiteSpace(ex.Note) ? null : ex.Note.Trim()
            });
        }

        _dbContext.WorkoutPlans.Add(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Entry(plan)
            .Collection(p => p.Exercises)
            .Query()
            .Include(e => e.Exercise)
            .LoadAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_WORKOUT_PLAN", "WorkoutPlan", plan.Id,
            new { memberId, trainerId = trainerProfile.Id }, cancellationToken);

        return ServiceResult<WorkoutPlanResponse>.Success(
            ToResponse(plan, memberProfile, trainerProfile),
            StatusCodes.Status201Created);
    }

    // FR-WP-02: PT sua bai tap trong plan cua minh.
    public async Task<ServiceResult<WorkoutPlanResponse>> UpdateAsync(
        long planId,
        UpdateWorkoutPlanRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var trainerProfile = await GetTrainerProfileAsync(principal, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<WorkoutPlanResponse>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var plan = await _dbContext.WorkoutPlans
            .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
            .Include(p => p.Member).ThenInclude(m => m.User)
            .Include(p => p.Trainer).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan is null)
        {
            return Fail<WorkoutPlanResponse>("NOT_FOUND", "Khong tim thay giao an.", StatusCodes.Status404NotFound);
        }

        if (plan.TrainerId != trainerProfile.Id)
        {
            return Fail<WorkoutPlanResponse>("FORBIDDEN", "Ban khong co quyen sua giao an nay.", StatusCodes.Status403Forbidden);
        }

        if (request.Exercises is not null && request.Exercises.Count == 0)
        {
            return Fail<WorkoutPlanResponse>("EMPTY_PLAN", "Giao an phai co it nhat 1 bai tap.", StatusCodes.Status422UnprocessableEntity);
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            plan.Title = request.Title.Trim();
        }

        if (request.Goal is not null)
        {
            plan.Goal = string.IsNullOrWhiteSpace(request.Goal) ? null : request.Goal.Trim();
        }

        if (request.StartDate is not null)
        {
            plan.StartDate = request.StartDate.Value;
        }

        if (request.EndDate is not null)
        {
            plan.EndDate = request.EndDate.Value;
        }

        if (request.Status is not null)
        {
            plan.Status = (byte)request.Status.Value;
        }

        if (request.Exercises is not null)
        {
            var exerciseMap = await ResolveExercisesAsync(request.Exercises, cancellationToken);

            _dbContext.WorkoutExercises.RemoveRange(plan.Exercises);
            plan.Exercises.Clear();

            for (var i = 0; i < request.Exercises.Count; i++)
            {
                var ex = request.Exercises[i];
                var exerciseId = exerciseMap[ex.Name.Trim()];
                plan.Exercises.Add(new WorkoutExercise
                {
                    WorkoutPlanId = plan.Id,
                    ExerciseId = exerciseId,
                    // Xem ghi chu o CreateAsync: SortOrder theo vi tri list, tranh trung key.
                    SortOrder = (short)(i + 1),
                    Sets = ex.Sets,
                    Reps = ParseReps(ex.Reps),
                    Note = string.IsNullOrWhiteSpace(ex.Note) ? null : ex.Note.Trim()
                });
            }
        }

        plan.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.Exercises is not null)
        {
            await _dbContext.Entry(plan)
                .Collection(p => p.Exercises)
                .Query()
                .Include(e => e.Exercise)
                .LoadAsync(cancellationToken);
        }

        await _auditService.LogAsync("UPDATE_WORKOUT_PLAN", "WorkoutPlan", plan.Id, null, cancellationToken);

        return ServiceResult<WorkoutPlanResponse>.Success(ToResponse(plan, plan.Member, trainerProfile));
    }

    // FR-WP-03: PT xoa plan cua minh.
    public async Task<ServiceResult<object?>> DeleteAsync(
        long planId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var trainerProfile = await GetTrainerProfileAsync(principal, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<object?>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var plan = await _dbContext.WorkoutPlans
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan is null)
        {
            return Fail<object?>("NOT_FOUND", "Khong tim thay giao an.", StatusCodes.Status404NotFound);
        }

        if (plan.TrainerId != trainerProfile.Id)
        {
            return Fail<object?>("FORBIDDEN", "Ban khong co quyen xoa giao an nay.", StatusCodes.Status403Forbidden);
        }

        if (plan.Exercises.Count > 0)
        {
            _dbContext.WorkoutExercises.RemoveRange(plan.Exercises);
        }

        _dbContext.WorkoutPlans.Remove(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("DELETE_WORKOUT_PLAN", "WorkoutPlan", planId, null, cancellationToken);

        return ServiceResult<object?>.Success(null, StatusCodes.Status204NoContent);
    }

    // FR-PT-04 / AC-06: PT/Admin xem tat ca plan; Member chi xem cua minh.
    public async Task<ServiceResult<IReadOnlyList<WorkoutPlanResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var memberProfile = await _dbContext.MemberProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == memberId && !p.IsDeleted, cancellationToken);

        if (memberProfile is null)
        {
            return Fail<IReadOnlyList<WorkoutPlanResponse>>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        var userId = GetActorId(principal);

        if (principal.IsInRole(RoleNames.Member))
        {
            if (userId != memberProfile.UserId)
            {
                return Fail<IReadOnlyList<WorkoutPlanResponse>>("FORBIDDEN", "Ban khong co quyen xem giao an nay.", StatusCodes.Status403Forbidden);
            }
        }
        else if (principal.IsInRole(RoleNames.Pt))
        {
            var trainerProfile = await _dbContext.TrainerProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

            if (trainerProfile is null)
            {
                return Fail<IReadOnlyList<WorkoutPlanResponse>>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
            }

            var isAssigned = await _dbContext.TrainerAssignments.AnyAsync(
                a => a.TrainerId == trainerProfile.Id && a.MemberId == memberId && a.Status == AssignmentStatuses.Active,
                cancellationToken);

            if (!isAssigned)
            {
                return Fail<IReadOnlyList<WorkoutPlanResponse>>("FORBIDDEN", "PT chua duoc phan cong cho hoi vien nay.", StatusCodes.Status403Forbidden);
            }
        }

        var plans = await _dbContext.WorkoutPlans
            .Include(p => p.Exercises).ThenInclude(e => e.Exercise)
            .Include(p => p.Trainer).ThenInclude(t => t.User)
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = plans.Select(p => ToResponse(p, memberProfile, p.Trainer)).ToList();

        return ServiceResult<IReadOnlyList<WorkoutPlanResponse>>.Success(result);
    }

    // Resolve exercise names to catalog IDs, creating missing entries on the fly.
    private async Task<Dictionary<string, long>> ResolveExercisesAsync(
        IReadOnlyList<WorkoutExerciseRequest> exercises,
        CancellationToken ct)
    {
        var names = exercises.Select(e => e.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // exercise_catalog.Name la UNIQUE + collation khong phan biet hoa/thuong.
        // Phai dung StringComparer.OrdinalIgnoreCase de khop voi SQL, neu khong
        // C# (ordinal) tuong la bai moi -> INSERT trung (vd "squat" vs "Squat")
        // -> Violation of UNIQUE KEY -> SaveChanges throw -> 500.
        var existing = await _dbContext.ExerciseCatalogs
            .Where(e => names.Contains(e.Name))
            .ToDictionaryAsync(e => e.Name, StringComparer.OrdinalIgnoreCase, ct);

        var toCreate = names
            .Where(n => !existing.ContainsKey(n))
            .Select(n => new ExerciseCatalog { Name = n, IsActive = true })
            .ToList();

        if (toCreate.Count > 0)
        {
            _dbContext.ExerciseCatalogs.AddRange(toCreate);
            await _dbContext.SaveChangesAsync(ct);
            foreach (var e in toCreate)
                existing[e.Name] = e;
        }

        return existing.ToDictionary(
            kvp => kvp.Key, kvp => kvp.Value.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<TrainerProfile?> GetTrainerProfileAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = GetActorId(principal);
        if (userId is null) return null;

        return await _dbContext.TrainerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);
    }

    private async Task<MemberProfile?> GetAssignedMemberAsync(long trainerId, long memberId, CancellationToken ct)
    {
        var isAssigned = await _dbContext.TrainerAssignments.AnyAsync(
            a => a.TrainerId == trainerId && a.MemberId == memberId && a.Status == AssignmentStatuses.Active, ct);

        if (!isAssigned) return null;

        return await _dbContext.MemberProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == memberId && !p.IsDeleted, ct);
    }

    private static WorkoutPlanResponse ToResponse(WorkoutPlan plan, MemberProfile member, TrainerProfile trainer)
    {
        var statusStr = plan.Status switch
        {
            WorkoutPlanStatuses.Completed => "completed",
            WorkoutPlanStatuses.Cancelled => "cancelled",
            _ => "active"
        };

        return new WorkoutPlanResponse(
            plan.Id,
            plan.MemberId,
            member.User.FullName,
            plan.TrainerId,
            trainer.User.FullName,
            plan.Title,
            plan.Goal,
            plan.StartDate,
            plan.EndDate,
            statusStr,
            plan.CreatedAt,
            plan.UpdatedAt,
            plan.Exercises
                .OrderBy(e => e.SortOrder)
                .Select(e => new WorkoutExerciseResponse(
                    e.Id,
                    e.Exercise?.Name ?? string.Empty,
                    e.Exercise?.MuscleGroup,
                    e.SortOrder,
                    e.Sets,
                    e.Reps.HasValue ? e.Reps.Value.ToString() : null,
                    e.Note))
                .ToList());
    }

    private static short? ParseReps(string? reps)
    {
        if (string.IsNullOrWhiteSpace(reps)) return null;
        var first = reps.Split('-', '×', 'x')[0].Trim();
        return short.TryParse(first, out var val) ? val : null;
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static ServiceResult<T> Fail<T>(string code, string message, int statusCode)
        => ServiceResult<T>.Failure(code, message, statusCode);
}
