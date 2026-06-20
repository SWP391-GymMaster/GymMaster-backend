using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class AssignmentService : IAssignmentService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AssignmentService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-PT-01 / FR-PT-02: phan cong PT cho Member; dong assignment cu neu co.
    public async Task<AuthServiceResult<TrainerAssignmentResponse>> AssignAsync(
        AssignTrainerRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);

        if (actorId is null)
        {
            return Fail<TrainerAssignmentResponse>("UNAUTHORIZED", "Khong xac dinh duoc danh tinh.", StatusCodes.Status401Unauthorized);
        }

        var memberProfile = await _dbContext.MemberProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == request.MemberId && !p.IsDeleted, cancellationToken);

        if (memberProfile is null)
        {
            return Fail<TrainerAssignmentResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        var trainerProfile = await _dbContext.TrainerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == request.TrainerId && !p.IsDeleted, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<TrainerAssignmentResponse>("NOT_FOUND", "Khong tim thay PT.", StatusCodes.Status404NotFound);
        }

        // FR-PT-02: dong assignment cu neu member da co PT active.
        var existing = await _dbContext.TrainerAssignments
            .FirstOrDefaultAsync(a =>
                a.MemberId == request.MemberId && a.Status == AssignmentStatuses.Active, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (existing is not null)
        {
            existing.Status = AssignmentStatuses.Ended;
            existing.EndDate = today;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        var assignment = new TrainerAssignment
        {
            MemberId = request.MemberId,
            TrainerId = request.TrainerId,
            Status = AssignmentStatuses.Active,
            StartDate = today,
            CreatedByUserId = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TrainerAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "ASSIGN_PT",
            "TrainerAssignment",
            assignment.Id,
            new { memberId = request.MemberId, trainerId = request.TrainerId, replacedId = existing?.Id },
            cancellationToken);

        return AuthServiceResult<TrainerAssignmentResponse>.Success(
            ToAssignmentResponse(assignment, memberProfile, trainerProfile),
            StatusCodes.Status201Created);
    }

    // FR-PT-01/02: lay danh sach member de admin chon phan cong PT.
    public async Task<AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateMemberResponse>>> GetCandidateMembersAsync(
        string? query,
        bool includeAssigned,
        CancellationToken cancellationToken)
    {
        var members = _dbContext.MemberProfiles
            .Include(m => m.User)
            .Where(m => !m.IsDeleted && !m.User.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            members = members.Where(m =>
                m.User.FullName.Contains(keyword) ||
                m.User.Email.Contains(keyword) ||
                (m.User.Phone != null && m.User.Phone.Contains(keyword)));
        }

        var memberList = await members.ToListAsync(cancellationToken);

        var activeAssignments = await _dbContext.TrainerAssignments
            .Include(a => a.Trainer)
                .ThenInclude(t => t.User)
            .Where(a => a.Status == AssignmentStatuses.Active)
            .ToListAsync(cancellationToken);

        var assignmentByMember = activeAssignments.ToDictionary(a => a.MemberId);

        var result = memberList
            .Where(m => includeAssigned || !assignmentByMember.ContainsKey(m.Id))
            .Select(m =>
            {
                assignmentByMember.TryGetValue(m.Id, out var assignment);
                return new AssignmentCandidateMemberResponse(
                    m.Id,
                    $"MEM-{m.Id:D6}",
                    m.User.FullName,
                    m.User.Email,
                    m.User.Phone,
                    m.User.Status == "active" ? "active" : "pending",
                    null,
                    assignment?.TrainerId,
                    assignment?.Trainer.User.FullName,
                    "normal");
            })
            .ToList();

        return AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateMemberResponse>>.Success(
            new AssignmentCandidateListResponse<AssignmentCandidateMemberResponse>(result, result.Count));
    }

    // FR-PT-01: lay danh sach PT de admin chon phan cong.
    public async Task<AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateTrainerResponse>>> GetCandidateTrainersAsync(
        string? query,
        string? specialty,
        CancellationToken cancellationToken)
    {
        var trainers = _dbContext.TrainerProfiles
            .Include(t => t.User)
            .Where(t => !t.IsDeleted && !t.User.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            trainers = trainers.Where(t =>
                t.User.FullName.Contains(keyword) ||
                t.User.Email.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(specialty))
        {
            var spec = specialty.Trim();
            trainers = trainers.Where(t => t.Specialty != null && t.Specialty.Contains(spec));
        }

        var trainerList = await trainers.ToListAsync(cancellationToken);

        var assignedCounts = await _dbContext.TrainerAssignments
            .Where(a => a.Status == AssignmentStatuses.Active)
            .GroupBy(a => a.TrainerId)
            .Select(g => new { TrainerId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countByTrainer = assignedCounts.ToDictionary(x => x.TrainerId, x => x.Count);

        var result = trainerList.Select(t => new AssignmentCandidateTrainerResponse(
            t.Id,
            t.UserId,
            t.User.FullName,
            t.User.Status,
            t.Specialty,
            countByTrainer.GetValueOrDefault(t.Id, 0),
            10)).ToList();

        return AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateTrainerResponse>>.Success(
            new AssignmentCandidateListResponse<AssignmentCandidateTrainerResponse>(result, result.Count));
    }

    // AC-03: PT chi thay member duoc phan cong.
    public async Task<AuthServiceResult<IReadOnlyList<AssignedMemberResponse>>> GetAssignedMembersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetActorId(principal);

        if (userId is null)
        {
            return Fail<IReadOnlyList<AssignedMemberResponse>>("UNAUTHORIZED", "Khong xac dinh duoc danh tinh.", StatusCodes.Status401Unauthorized);
        }

        var trainerProfile = await _dbContext.TrainerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<IReadOnlyList<AssignedMemberResponse>>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var assignments = await _dbContext.TrainerAssignments
            .Include(a => a.Member)
                .ThenInclude(m => m.User)
            .Where(a => a.TrainerId == trainerProfile.Id && a.Status == AssignmentStatuses.Active)
            .OrderBy(a => a.StartDate)
            .ToListAsync(cancellationToken);

        var result = assignments.Select(a => new AssignedMemberResponse(
            a.MemberId,
            $"MEM-{a.MemberId:D6}",
            a.Member.User.FullName,
            a.Member.User.Email,
            a.Member.User.Phone,
            a.Member.User.Status,
            a.StartDate)).ToList();

        return AuthServiceResult<IReadOnlyList<AssignedMemberResponse>>.Success(result);
    }

    private static TrainerAssignmentResponse ToAssignmentResponse(
        TrainerAssignment assignment,
        MemberProfile member,
        TrainerProfile trainer)
    {
        return new TrainerAssignmentResponse(
            assignment.Id,
            assignment.MemberId,
            member.User.FullName,
            assignment.TrainerId,
            trainer.User.FullName,
            assignment.Status,
            assignment.StartDate,
            assignment.EndDate,
            assignment.CreatedAt);
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out var id) ? id : null;
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
        => AuthServiceResult<T>.Failure(code, message, statusCode);
}
