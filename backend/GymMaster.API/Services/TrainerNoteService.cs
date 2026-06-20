using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class TrainerNoteService : ITrainerNoteService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TrainerNoteService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-NOTE-01: PT ghi note cho member duoc phan cong.
    public async Task<AuthServiceResult<TrainerNoteResponse>> CreateAsync(
        long memberId,
        CreateTrainerNoteRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Fail<TrainerNoteResponse>("VALIDATION_ERROR", "Noi dung ghi chu khong duoc de trong.", StatusCodes.Status400BadRequest);
        }

        var userId = GetActorId(principal);

        if (userId is null)
        {
            return Fail<TrainerNoteResponse>("UNAUTHORIZED", "Khong xac dinh duoc danh tinh.", StatusCodes.Status401Unauthorized);
        }

        var trainerProfile = await _dbContext.TrainerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<TrainerNoteResponse>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        // FR-PT-03: kiem tra PT co duoc phan cong cho member nay khong.
        var memberProfile = await _dbContext.MemberProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == memberId && !p.IsDeleted, cancellationToken);

        if (memberProfile is null)
        {
            return Fail<TrainerNoteResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        var isAssigned = await _dbContext.TrainerAssignments.AnyAsync(
            a => a.TrainerId == trainerProfile.Id && a.MemberId == memberId && a.Status == AssignmentStatuses.Active,
            cancellationToken);

        if (!isAssigned)
        {
            return Fail<TrainerNoteResponse>("FORBIDDEN", "PT chua duoc phan cong cho hoi vien nay.", StatusCodes.Status403Forbidden);
        }

        var note = new TrainerNote
        {
            TrainerId = trainerProfile.Id,
            MemberId = memberId,
            NoteDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Content = request.Content.Trim(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TrainerNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_TRAINER_NOTE", "TrainerNote", note.Id,
            new { memberId, trainerId = trainerProfile.Id }, cancellationToken);

        return AuthServiceResult<TrainerNoteResponse>.Success(
            ToResponse(note, memberProfile, trainerProfile),
            StatusCodes.Status201Created);
    }

    // FR-PT-04: Member chi xem ghi chu cua minh; PT chi xem member duoc phan cong; Admin xem tat ca.
    public async Task<AuthServiceResult<IReadOnlyList<TrainerNoteResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var memberProfile = await _dbContext.MemberProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == memberId && !p.IsDeleted, cancellationToken);

        if (memberProfile is null)
        {
            return Fail<IReadOnlyList<TrainerNoteResponse>>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        var userId = GetActorId(principal);

        if (principal.IsInRole(RoleNames.Member))
        {
            if (userId != memberProfile.UserId)
            {
                return Fail<IReadOnlyList<TrainerNoteResponse>>("FORBIDDEN", "Ban khong co quyen xem ghi chu nay.", StatusCodes.Status403Forbidden);
            }
        }
        else if (principal.IsInRole(RoleNames.Pt))
        {
            var trainerProfile = await _dbContext.TrainerProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);

            if (trainerProfile is null)
            {
                return Fail<IReadOnlyList<TrainerNoteResponse>>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
            }

            var isAssigned = await _dbContext.TrainerAssignments.AnyAsync(
                a => a.TrainerId == trainerProfile.Id && a.MemberId == memberId && a.Status == AssignmentStatuses.Active,
                cancellationToken);

            if (!isAssigned)
            {
                return Fail<IReadOnlyList<TrainerNoteResponse>>("FORBIDDEN", "PT chua duoc phan cong cho hoi vien nay.", StatusCodes.Status403Forbidden);
            }
        }

        var notes = await _dbContext.TrainerNotes
            .Include(n => n.Trainer).ThenInclude(t => t.User)
            .Where(n => n.MemberId == memberId)
            .OrderByDescending(n => n.NoteDate)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = notes.Select(n => ToResponse(n, memberProfile, n.Trainer)).ToList();

        return AuthServiceResult<IReadOnlyList<TrainerNoteResponse>>.Success(result);
    }

    private static TrainerNoteResponse ToResponse(TrainerNote note, MemberProfile member, TrainerProfile trainer)
    {
        return new TrainerNoteResponse(
            note.Id,
            note.MemberId,
            member.User.FullName,
            note.TrainerId,
            trainer.User.FullName,
            note.NoteDate,
            note.Content,
            note.CreatedAt);
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
