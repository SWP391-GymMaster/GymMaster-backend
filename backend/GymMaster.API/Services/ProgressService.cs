using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class ProgressService : IProgressService
{
    private const int NoteMaxLength = 500;

    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public ProgressService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-PROG-01
    public async Task<AuthServiceResult<ProgressResponse>> RecordAsync(
        long memberId,
        RecordProgressRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<ProgressResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<ProgressResponse>("FORBIDDEN", "Ban khong co quyen ghi tien do nay.", StatusCodes.Status403Forbidden);
        }

        if (!HasAnyMeasurement(request) ||
            !IsInRange(request.WeightKg, 20, 300) ||
            !IsInRange(request.BodyFatPercent, 0, 70) ||
            !IsInRange(request.ChestCm, 30, 200) ||
            !IsInRange(request.WaistCm, 30, 200) ||
            !IsInRange(request.HipCm, 30, 200))
        {
            return Fail<ProgressResponse>(
                "INVALID_MEASUREMENT",
                "Chi so tien do khong hop le.",
                StatusCodes.Status422UnprocessableEntity);
        }

        var measuredAt = request.MeasuredAt ?? DateTime.UtcNow;
        if (measuredAt > DateTime.UtcNow)
        {
            return Fail<ProgressResponse>(
                "INVALID_MEASUREMENT",
                "Thoi diem do khong duoc o tuong lai.",
                StatusCodes.Status422UnprocessableEntity);
        }

        if (request.Note is not null && request.Note.Length > NoteMaxLength)
        {
            return Fail<ProgressResponse>(
                "INVALID_MEASUREMENT",
                "Ghi chu tien do qua dai.",
                StatusCodes.Status422UnprocessableEntity);
        }

        var log = new ProgressLog
        {
            MemberId = profile.Id,
            MeasuredAt = measuredAt,
            WeightKg = request.WeightKg,
            BodyFatPercent = request.BodyFatPercent,
            ChestCm = request.ChestCm,
            WaistCm = request.WaistCm,
            HipCm = request.HipCm,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedByUserId = GetActorId(principal),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProgressLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_PROGRESS", "ProgressLog", log.Id, new { memberId }, cancellationToken);

        return AuthServiceResult<ProgressResponse>.Success(
            ToResponse(log),
            StatusCodes.Status201Created);
    }

    // FR-PROG-03
    public async Task<AuthServiceResult<IReadOnlyList<ProgressResponse>>> GetTimelineAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<ProgressResponse>>(
                "NOT_FOUND",
                "Khong tim thay hoi vien.",
                StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<IReadOnlyList<ProgressResponse>>(
                "FORBIDDEN",
                "Ban khong co quyen xem tien do nay.",
                StatusCodes.Status403Forbidden);
        }

        var logs = await _dbContext.ProgressLogs
            .Where(item => item.MemberId == memberId)
            .OrderBy(item => item.MeasuredAt)
            .ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<ProgressResponse>>.Success(
            logs.Select(ToResponse).ToList());
    }

    // FR-360-01
    public async Task<AuthServiceResult<Profile360Response>> GetProfile360Async(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await FindMemberAsync(memberId, cancellationToken);

        if (profile is null)
        {
            return Fail<Profile360Response>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<Profile360Response>("FORBIDDEN", "Ban khong co quyen xem ho so 360 nay.", StatusCodes.Status403Forbidden);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var memberships = await _dbContext.Memberships
            .Include(item => item.Package)
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        // Dong bo trang thai het han (lazy) giong cac endpoint membership khac (FR-MS-07),
        // tranh 360 hien Status "Active" cho membership da qua EndDate.
        if (ExpireIfPastDue(memberships, today))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var currentMembership = memberships.FirstOrDefault(
            item => item.Status == MembershipStatus.Active && item.EndDate >= today);

        var progress = await _dbContext.ProgressLogs
            .Where(item => item.MemberId == memberId)
            .OrderBy(item => item.MeasuredAt)
            .ToListAsync(cancellationToken);

        var response = new Profile360Response(
            new MemberInfo(
                profile.Id,
                profile.UserId,
                profile.User.FullName,
                profile.User.Email,
                profile.User.Phone,
                profile.DateOfBirth,
                profile.Gender),
            currentMembership is null ? null : ToMembershipResponse(currentMembership),
            memberships.Select(item => (object)ToMembershipResponse(item)).ToList(),
            progress.Select(ToResponse).ToList(),
            null,
            null,
            null);

        return AuthServiceResult<Profile360Response>.Success(response);
    }

    private Task<MemberProfile?> FindMemberAsync(long memberId, CancellationToken cancellationToken)
    {
        return _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted, cancellationToken);
    }

    private static bool CanAccess(ClaimsPrincipal principal, MemberProfile profile)
    {
        if (principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.Staff))
        {
            return true;
        }

        if (principal.IsInRole(RoleNames.Member))
        {
            return GetActorId(principal) == profile.UserId;
        }

        return false;
    }

    private static bool HasAnyMeasurement(RecordProgressRequest request)
    {
        return request.WeightKg is not null ||
            request.BodyFatPercent is not null ||
            request.ChestCm is not null ||
            request.WaistCm is not null ||
            request.HipCm is not null;
    }

    private static bool IsInRange(decimal? value, decimal min, decimal max)
    {
        return value is null || (value >= min && value <= max);
    }

    // Lazy-expire: chuyen membership da qua EndDate tu Active -> Expired (FR-MS-07).
    // Tra ve true neu co thay doi de caller quyet dinh co SaveChanges hay khong.
    private static bool ExpireIfPastDue(IEnumerable<Membership> memberships, DateOnly today)
    {
        var changed = false;
        foreach (var membership in memberships.Where(item => item.Status == MembershipStatus.Active && item.EndDate < today))
        {
            membership.Status = MembershipStatus.Expired;
            membership.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        return changed;
    }

    private static ProgressResponse ToResponse(ProgressLog log)
    {
        return new ProgressResponse(
            log.Id,
            log.MemberId,
            log.MeasuredAt,
            log.WeightKg,
            log.BodyFatPercent,
            log.ChestCm,
            log.WaistCm,
            log.HipCm,
            log.Note,
            log.CreatedAt);
    }

    private static MembershipResponse ToMembershipResponse(Membership membership)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysRemaining = membership.EndDate.DayNumber - today.DayNumber;

        return new MembershipResponse(
            membership.Id,
            membership.MemberId,
            membership.PackageId,
            membership.Package.Name,
            membership.StartDate,
            membership.EndDate,
            membership.Status.ToString(),
            daysRemaining,
            membership.Status == MembershipStatus.Active && daysRemaining is >= 0 and <= 7,
            membership.CreatedAt);
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
