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

        // FR-CHK-03: gioi han so lan check-in trong ngay (mac dinh 2 lan/ngay).
        var dailyLimitError = await ValidateDailyLimitAsync(profile.Id, cancellationToken);
        if (dailyLimitError is not null)
        {
            return dailyLimitError;
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
            // `date` la ngay lich theo gio VN -> quy ve khung [dau, cuoi) UTC,
            // nhat quan voi AppClock/OncePerDay (khong lech 1 ngay vao sang gio VN).
            var (start, end) = DayUtcRangeVn(date.Value);
            query = query.Where(checkIn => checkIn.CheckInAt >= start && checkIn.CheckInAt < end);
        }

        // Kem ten hoi vien (qua subquery) de FE hien "check-in gan day" — giong
        // pattern RecentlyExpired cua dashboard. CheckIn khong co navigation -> join theo MemberId.
        var rows = await query
            .OrderByDescending(checkIn => checkIn.CheckInAt)
            .Select(checkIn => new CheckInRow(
                checkIn.Id,
                checkIn.MemberId,
                checkIn.CheckInAt,
                checkIn.CreatedBy,
                _dbContext.MemberProfiles
                    .Where(profile => profile.Id == checkIn.MemberId)
                    .Select(profile => profile.User.FullName)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<CheckInResponse>>.Success(
            rows.Select(ToResponse).ToList());
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

    // Spec 005 — PT check-in cho hoi vien duoc phan cong cho minh.
    // Ownership: PT CHI duoc check-in member dang co assignment Active voi minh (khong cham member cua PT khac).
    public async Task<AuthServiceResult<CheckInResponse>> CreateForAssignedMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorId(principal);

        if (actorUserId is null)
        {
            return Fail("UNAUTHORIZED", "Khong xac dinh duoc danh tinh.", StatusCodes.Status401Unauthorized);
        }

        var trainerProfile = await _dbContext.TrainerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == actorUserId && !p.IsDeleted, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail("TRAINER_NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var profile = await FindProfileAsync(p => p.Id == memberId, cancellationToken);

        // FR-CHK-04: Id khong khop Member nao.
        if (profile is null)
        {
            return Fail("MEMBER_NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        // Ownership (yeu cau cot loi): PT chi check-in hoi vien duoc phan cong cho minh.
        var isAssigned = await _dbContext.TrainerAssignments.AnyAsync(
            a => a.TrainerId == trainerProfile.Id
                && a.MemberId == memberId
                && a.Status == AssignmentStatuses.Active,
            cancellationToken);

        if (!isAssigned)
        {
            return Fail(
                "FORBIDDEN",
                "PT chi co the check-in cho hoi vien duoc phan cong cho minh.",
                StatusCodes.Status403Forbidden);
        }

        // FR-CHK-05: tai khoan bi khoa -> tu choi.
        if (IsAccountLocked(profile.User))
        {
            return Fail("ACCOUNT_LOCKED", "Tai khoan dang bi khoa, khong the check-in.", StatusCodes.Status403Forbidden);
        }

        // FR-CHK-02/03: dung chung quy tac membership voi luong staff/member (theo cau hinh CheckIn:EnforceMembership).
        if (_options.EnforceMembership)
        {
            var membershipError = await ValidateMembershipAsync(profile.Id, cancellationToken);
            if (membershipError is not null)
            {
                return membershipError;
            }
        }

        // FR-CHK-03: gioi han so lan check-in trong ngay (mac dinh 2 lan/ngay).
        var dailyLimitError = await ValidateDailyLimitAsync(profile.Id, cancellationToken);
        if (dailyLimitError is not null)
        {
            return dailyLimitError;
        }

        // CreatedBy = userId cua PT (nguoi thuc hien) — giong nguyen tac staff tac nghiep ho.
        var checkIn = new CheckIn
        {
            MemberId = profile.Id,
            CheckInAt = DateTime.UtcNow,
            CreatedBy = actorUserId
        };

        _dbContext.CheckIns.Add(checkIn);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "CREATE_CHECKIN", "CheckIn", checkIn.Id, new { memberId = profile.Id, source = "pt" }, cancellationToken);

        return AuthServiceResult<CheckInResponse>.Success(ToResponse(checkIn), StatusCodes.Status201Created);
    }

    // GET /api/v1/pt/checkins/today — check-in HOM NAY cua cac hoi vien duoc phan cong cho PT.
    public async Task<AuthServiceResult<IReadOnlyList<CheckInResponse>>> ListTodayForTrainerAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetActorId(principal);

        if (actorUserId is null)
        {
            return Fail<IReadOnlyList<CheckInResponse>>(
                "UNAUTHORIZED", "Khong xac dinh duoc danh tinh.", StatusCodes.Status401Unauthorized);
        }

        var trainerProfile = await _dbContext.TrainerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == actorUserId && !p.IsDeleted, cancellationToken);

        if (trainerProfile is null)
        {
            return Fail<IReadOnlyList<CheckInResponse>>(
                "TRAINER_NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        var assignedMemberIds = await _dbContext.TrainerAssignments
            .Where(a => a.TrainerId == trainerProfile.Id && a.Status == AssignmentStatuses.Active)
            .Select(a => a.MemberId)
            .ToListAsync(cancellationToken);

        if (assignedMemberIds.Count == 0)
        {
            return Ok(Array.Empty<CheckIn>());
        }

        var (start, end) = TodayUtcRangeVn();

        var items = await _dbContext.CheckIns
            .AsNoTracking()
            .Where(checkIn => assignedMemberIds.Contains(checkIn.MemberId)
                && checkIn.CheckInAt >= start
                && checkIn.CheckInAt < end)
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
        // Theo gio VN (AppClock) de khong lech 1 ngay han membership vao sang gio VN.
        var today = AppClock.Today();

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

    // Khung [dau, cuoi) cua "hom nay" theo gio VN (GMT+7, xem AppClock), quy ve UTC
    // de so sanh voi CheckInAt (luu UTC). Nho do "ngay" reset luc nua dem gio VN,
    // khong phai 7h sang (= 0h UTC).
    private static (DateTime StartUtc, DateTime EndUtc) TodayUtcRangeVn()
        => DayUtcRangeVn(AppClock.Today());

    // Khung [dau, cuoi) UTC cua mot ngay lich VN bat ky.
    private static (DateTime StartUtc, DateTime EndUtc) DayUtcRangeVn(DateOnly dateVn)
    {
        var startVn = dateVn.ToDateTime(TimeOnly.MinValue);
        var startUtc = startVn.AddHours(-7);
        return (startUtc, startUtc.AddDays(1));
    }

    private Task<int> CountCheckedInTodayAsync(long memberId, CancellationToken cancellationToken)
    {
        var (start, end) = TodayUtcRangeVn();

        return _dbContext.CheckIns.CountAsync(
            checkIn => checkIn.MemberId == memberId && checkIn.CheckInAt >= start && checkIn.CheckInAt < end,
            cancellationToken);
    }

    // FR-CHK-03: tra ve loi neu da dat gioi han check-in trong ngay; null neu con luot.
    private async Task<AuthServiceResult<CheckInResponse>?> ValidateDailyLimitAsync(
        long memberId,
        CancellationToken cancellationToken)
    {
        var limit = _options.EffectiveMaxPerDay;
        if (limit <= 0)
        {
            return null; // khong gioi han
        }

        var count = await CountCheckedInTodayAsync(memberId, cancellationToken);
        if (count >= limit)
        {
            return Fail(
                "DAILY_LIMIT_REACHED",
                $"Hoi vien da check-in du {limit} lan trong hom nay.",
                StatusCodes.Status409Conflict);
        }

        return null;
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
        => ToResponse(new CheckInRow(checkIn.Id, checkIn.MemberId, checkIn.CheckInAt, checkIn.CreatedBy, null));

    private static CheckInResponse ToResponse(CheckInRow row)
    {
        // FR-CHK-06: CreatedBy != null => nhan vien thuc hien ("front-desk"); null => member tu check-in.
        var source = row.CreatedBy is null ? "member" : "front-desk";

        // NFR-02: DATETIME2 doc tu DB co Kind=Unspecified -> ep ve UTC de serialize kem 'Z',
        // FE new Date(checkInAt) doi dung gio dia phuong.
        var checkInAtUtc = DateTime.SpecifyKind(row.CheckInAt, DateTimeKind.Utc);
        return new CheckInResponse(row.Id, row.MemberId, checkInAtUtc, source, row.MemberName);
    }

    // Hang phang doc tu DB cho cac endpoint LIST (kem ten hoi vien de hien o FE).
    private sealed record CheckInRow(
        long Id,
        long MemberId,
        DateTime CheckInAt,
        long? CreatedBy,
        string? MemberName);

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
