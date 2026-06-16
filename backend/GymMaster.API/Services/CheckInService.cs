using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class CheckInService : ICheckInService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly CheckInOptions _options;

    public CheckInService(
        GymMasterDbContext dbContext,
        IAuditService auditService,
        IOptions<CheckInOptions> options)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _options = options.Value;
    }

    // FR-CHK-01..06: tao 1 luot check-in sau khi xac thuc.
    public async Task<AuthServiceResult<CheckInResponse>> CreateAsync(
        CreateCheckInRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorId(principal);

        // Member tu check-in: chi co role member (khong phai staff/admin tac nghiep ho).
        var isMemberSelf = principal.IsInRole(RoleNames.Member)
            && !principal.IsInRole(RoleNames.Staff)
            && !principal.IsInRole(RoleNames.Admin);

        var profile = await ResolveMemberAsync(request, isMemberSelf, actorUserId, cancellationToken);

        // FR-CHK-04: ma/SDT/Id khong khop Member nao.
        if (profile is null)
        {
            return Fail("MEMBER_NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        // Member chi duoc tu check-in cho chinh minh (chong check-in ho nguoi khac).
        if (isMemberSelf && profile.UserId != actorUserId)
        {
            return Fail("FORBIDDEN", "Ban chi co the tu check-in cho chinh minh.", StatusCodes.Status403Forbidden);
        }

        // FR-CHK-05: tai khoan bi khoa -> tu choi.
        if (IsAccountLocked(profile.User))
        {
            return Fail("ACCOUNT_LOCKED", "Tai khoan dang bi khoa, khong the check-in.", StatusCodes.Status403Forbidden);
        }

        // FR-CHK-02/03: xac thuc membership Active con han (bat qua cau hinh CheckIn:EnforceMembership).
        if (_options.EnforceMembership)
        {
            var membershipError = await ValidateMembershipAsync(profile.Id, cancellationToken);
            if (membershipError is not null)
            {
                return membershipError;
            }
        }

        // FR-CHK-03 (OQ-06): tuy chon chan check-in thu 2 trong cung ngay.
        if (_options.OncePerDay && await HasCheckedInTodayAsync(profile.Id, cancellationToken))
        {
            return Fail("ALREADY_CHECKED_IN_TODAY", "Hoi vien da check-in trong hom nay.", StatusCodes.Status409Conflict);
        }

        // FR-CHK-01/06: tao ban ghi. CreatedByUserId = null khi member tu check-in.
        var checkIn = new CheckIn
        {
            MemberId = profile.Id,
            CheckInAt = DateTime.UtcNow,
            CreatedBy = isMemberSelf ? null : actorUserId
        };

        _dbContext.CheckIns.Add(checkIn);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "CREATE_CHECKIN", "CheckIn", checkIn.Id, new { memberId = profile.Id }, cancellationToken);

        return AuthServiceResult<CheckInResponse>.Success(ToResponse(checkIn), StatusCodes.Status201Created);
    }

    // GET /api/v1/checkins?date=&memberId=  (Admin, Staff)
    public async Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListAsync(
        DateOnly? date,
        long? memberId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CheckIns.AsNoTracking();

        if (memberId is not null)
        {
            query = query.Where(checkIn => checkIn.MemberId == memberId);
        }

        if (date is not null)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(checkIn => checkIn.CheckInAt >= start && checkIn.CheckInAt < end);
        }

        var items = await query
            .OrderByDescending(checkIn => checkIn.CheckInAt)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    // GET /api/v1/members/{id}/checkins  (Admin, Staff, PT, Member self)
    public async Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<CheckInResponse>>(
                "MEMBER_NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        // Ownership: Member chi xem cua minh; Admin/Staff/PT xem duoc (PT-assignment gac o spec 005).
        var isPrivileged = principal.IsInRole(RoleNames.Admin)
            || principal.IsInRole(RoleNames.Staff)
            || principal.IsInRole(RoleNames.Pt);

        if (!isPrivileged && GetActorId(principal) != profile.UserId)
        {
            return Fail<IReadOnlyList<CheckInResponse>>(
                "FORBIDDEN", "Ban khong co quyen xem du lieu nay.", StatusCodes.Status403Forbidden);
        }

        var items = await _dbContext.CheckIns
            .AsNoTracking()
            .Where(checkIn => checkIn.MemberId == memberId)
            .OrderByDescending(checkIn => checkIn.CheckInAt)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    // Tim ho so hoi vien theo MemberId / MemberCode / (member tu check-in -> chinh minh).
    private async Task<MemberProfile?> ResolveMemberAsync(
        CreateCheckInRequest request,
        bool isMemberSelf,
        long? actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.MemberId is not null)
        {
            return await FindProfileAsync(
                profile => profile.Id == request.MemberId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.MemberCode))
        {
            // Schema khong co ma rieng -> tra cuu theo SDT (users.Phone).
            var code = request.MemberCode.Trim();
            return await FindProfileAsync(
                profile => profile.User.Phone == code, cancellationToken);
        }

        // Khong truyen dinh danh: chi hop le khi member tu check-in cho minh.
        if (isMemberSelf && actorUserId is not null)
        {
            return await FindProfileAsync(
                profile => profile.UserId == actorUserId, cancellationToken);
        }

        return null;
    }

    private Task<MemberProfile?> FindProfileAsync(
        System.Linq.Expressions.Expression<Func<MemberProfile, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return _dbContext.MemberProfiles
            .Include(profile => profile.User)
            .Where(profile => !profile.IsDeleted && !profile.User.IsDeleted)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    // FR-CHK-02/03: tra ve loi tuong ung neu khong co goi Active con han; null neu hop le.
    private async Task<AuthServiceResult<CheckInResponse>?> ValidateMembershipAsync(
        long memberId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var memberships = await _dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.MemberId == memberId)
            .ToListAsync(cancellationToken);

        var hasActive = memberships.Any(membership =>
            membership.Status == MembershipStatus.Active && membership.EndDate >= today);

        if (hasActive)
        {
            return null;
        }

        // AC-03: con goi cho thanh toan -> PAYMENT_PENDING.
        if (memberships.Any(membership => membership.Status == MembershipStatus.PendingPayment))
        {
            return Fail(
                "PAYMENT_PENDING",
                "Goi hoi vien dang cho thanh toan. Vui long thanh toan truoc khi check-in.",
                StatusCodes.Status422UnprocessableEntity);
        }

        // AC-02: het han / khong co goi -> NO_ACTIVE_MEMBERSHIP + nhac gia han.
        return Fail(
            "NO_ACTIVE_MEMBERSHIP",
            "Hoi vien chua co goi tap dang hoat dong. Vui long gia han goi.",
            StatusCodes.Status422UnprocessableEntity);
    }

    private Task<bool> HasCheckedInTodayAsync(long memberId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow.Date;
        var end = start.AddDays(1);

        return _dbContext.CheckIns.AnyAsync(
            checkIn => checkIn.MemberId == memberId && checkIn.CheckInAt >= start && checkIn.CheckInAt < end,
            cancellationToken);
    }

    private static bool IsAccountLocked(User user)
    {
        if (string.Equals(user.Status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow;
    }

    private static AuthServiceResult<IReadOnlyList<CheckInResponse>> Ok(IEnumerable<CheckIn> items)
    {
        return AuthServiceResult<IReadOnlyList<CheckInResponse>>.Success(
            items.Select(ToResponse).ToList());
    }

    private static CheckInResponse ToResponse(CheckIn checkIn)
    {
        // FR-CHK-06: CreatedBy != null => nhan vien thuc hien ("front-desk"); null => member tu check-in.
        var source = checkIn.CreatedBy is null ? "member" : "front-desk";

        // NFR-02: DATETIME2 doc tu DB co Kind=Unspecified -> ep ve UTC de serialize kem 'Z',
        // FE new Date(checkInAt) doi dung gio dia phuong.
        var checkInAtUtc = DateTime.SpecifyKind(checkIn.CheckInAt, DateTimeKind.Utc);
        return new CheckInResponse(checkIn.Id, checkIn.MemberId, checkInAtUtc, source);
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static AuthServiceResult<CheckInResponse> Fail(string code, string message, int statusCode)
    {
        return AuthServiceResult<CheckInResponse>.Failure(code, message, statusCode);
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
