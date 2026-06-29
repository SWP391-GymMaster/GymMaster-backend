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
    private static readonly TimeSpan PendingPaymentTtl = TimeSpan.FromMinutes(30);

    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly INutritionService _nutritionService;

    public ProgressService(
        GymMasterDbContext dbContext,
        IAuditService auditService,
        INutritionService nutritionService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _nutritionService = nutritionService;
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
        {
            return Fail<ProgressResponse>("FORBIDDEN", "Ban khong co quyen ghi tien do nay.", StatusCodes.Status403Forbidden);
        }

        if (!HasAnyMeasurement(request) ||
            !IsInRange(request.WeightKg, 20, 300) ||
            !IsInRange(request.BodyFatPct, 0, 70) ||
            !IsInRange(request.ChestCm, 30, 200) ||
            !IsInRange(request.WaistCm, 30, 200) ||
            !IsInRange(request.HipCm, 30, 200))
        {
            return Fail<ProgressResponse>(
                "INVALID_MEASUREMENT",
                "Chi so tien do khong hop le.",
                StatusCodes.Status422UnprocessableEntity);
        }

        // FE gui ngay do theo lich VN; so voi "bay gio" gio VN de khong tu choi nham
        // ngay hom nay khi dang la rang sang gio VN (UTC van la hom truoc).
        var measuredAt = request.MeasuredAt ?? DateTime.UtcNow;
        if (measuredAt > AppClock.NowVn())
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

        // 1 ngay = 1 ban ghi: ghi nhan lai cung ngay -> DE LEN ban cu (update), khong tao
        // them diem moi tren bieu do. So sanh theo NGAY lich cua measuredAt (FE gui ngay
        // theo lich VN, gio 00:00) -> dung khoang [dayStart, dayEnd).
        var dayStart = measuredAt.Date;
        var dayEnd = dayStart.AddDays(1);
        var existing = await _dbContext.ProgressLogs
            .Where(item => item.MemberId == profile.Id
                && item.MeasuredAt >= dayStart && item.MeasuredAt < dayEnd)
            .OrderByDescending(item => item.MeasuredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isUpdate = existing is not null;
        var log = existing ?? new ProgressLog
        {
            MemberId = profile.Id,
            CreatedByUserId = GetActorId(principal),
            CreatedAt = DateTime.UtcNow
        };

        log.MeasuredAt = measuredAt;
        log.WeightKg = request.WeightKg;
        log.BodyFatPercent = request.BodyFatPct;
        log.ChestCm = request.ChestCm;
        log.WaistCm = request.WaistCm;
        log.HipCm = request.HipCm;
        log.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (!isUpdate)
        {
            _dbContext.ProgressLogs.Add(log);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            isUpdate ? "UPDATE_PROGRESS" : "CREATE_PROGRESS",
            "ProgressLog",
            log.Id,
            new { memberId, measuredOn = dayStart },
            cancellationToken);

        return AuthServiceResult<ProgressResponse>.Success(
            ToResponse(log),
            isUpdate ? StatusCodes.Status200OK : StatusCodes.Status201Created);
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
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

        if (!await CanAccessAsync(principal, profile, cancellationToken))
        {
            return Fail<Profile360Response>("FORBIDDEN", "Ban khong co quyen xem ho so 360 nay.", StatusCodes.Status403Forbidden);
        }

        var today = AppClock.Today();

        // 1) Membership: tat ca (moi nhat truoc) + dong bo trang thai het han.
        var memberships = await _dbContext.Memberships
            .Include(item => item.Package)
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (ExpireIfPastDue(memberships, today) | ExpireStalePending(memberships))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Membership nao da thanh toan (1 truy van, tranh N+1) de suy paymentStatus.
        var membershipIds = memberships.Select(item => item.Id).ToList();
        var paidIds = await _dbContext.Payments
            .Where(item => membershipIds.Contains(item.MembershipId) && item.Status == PaymentStatus.Paid)
            .Select(item => item.MembershipId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var paidSet = paidIds.ToHashSet();

        var membershipDtos = memberships.Select(item => ToMembership360(item, paidSet)).ToList();

        // "Goi hien tai" = nguon su that DUY NHAT: uu tien goi Active con han (EndDate lon nhat),
        // roi den don PendingPayment moi nhat, cuoi cung moi fallback dong moi tao nhat.
        // Tranh bốc nham dong Cancelled/cu lam "goi hien tai" (mau thuan voi lich su).
        var currentEntity = memberships
                .Where(item => item.Status == MembershipStatus.Active && item.EndDate >= today)
                .OrderByDescending(item => item.EndDate)
                .FirstOrDefault()
            ?? memberships
                .Where(item => item.Status == MembershipStatus.PendingPayment)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault()
            ?? memberships.FirstOrDefault();
        var currentMembershipDto = currentEntity is null
            ? null
            : membershipDtos.FirstOrDefault(item => item.Id == currentEntity.Id);

        // 2) Check-in gan nhat (chi DOC bang check_ins, khong sua code spec 004).
        var recentCheckInRows = await _dbContext.CheckIns
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.CheckInAt)
            .Take(5)
            .Select(item => new { item.Id, item.CheckInAt })
            .ToListAsync(cancellationToken);

        // NFR-02: datetime2 doc tu DB co Kind=Unspecified -> ep ve UTC de serialize kem 'Z',
        // FE new Date(checkInAt) moi doi dung gio dia phuong (dong bo voi CheckInService.ToResponse).
        var recentCheckIns = recentCheckInRows
            .Select(item => new CheckIn360(item.Id, DateTime.SpecifyKind(item.CheckInAt, DateTimeKind.Utc)))
            .ToList();

        // 3) Tien do (timeline tang dan theo ngay do).
        var progress = await _dbContext.ProgressLogs
            .Where(item => item.MemberId == memberId)
            .OrderBy(item => item.MeasuredAt)
            .ToListAsync(cancellationToken);

        // 4) Tom tat dinh duong hom nay (dung lai service 007 cua chinh Part Y).
        var nutrition = await _nutritionService.GetSummaryAsync(memberId, today, principal, cancellationToken);

        // 5) PT dang phan cong active (spec 005 da co bang trainer_assignments sau merge).
        var activeAssignment = await _dbContext.TrainerAssignments
            .Include(item => item.Trainer)
            .ThenInclude(trainer => trainer.User)
            .Where(item => item.MemberId == memberId && item.Status == AssignmentStatuses.Active)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var response = new Profile360Response(
            new Member360Info(
                profile.Id,
                $"MEM-{profile.Id:D6}",
                profile.User.FullName,
                profile.User.Email,
                profile.User.Phone,
                profile.User.Status,
                profile.DateOfBirth,
                profile.Gender),
            currentMembershipDto,
            membershipDtos,
            recentCheckIns,
            progress.Select(ToResponse).ToList(),
            nutrition.Succeeded ? nutrition.Value : null,
            activeAssignment is null
                ? null
                : new AssignedPt360(
                    activeAssignment.TrainerId,
                    activeAssignment.Trainer?.User?.FullName ?? "Huấn luyện viên",
                    activeAssignment.Trainer?.Specialty,
                    DateTime.SpecifyKind(activeAssignment.CreatedAt, DateTimeKind.Utc)));

        return AuthServiceResult<Profile360Response>.Success(response);
    }

    // Mot membership -> DTO 360. Status/PaymentStatus giu nguyen ten enum (PascalCase)
    // dong bo voi spec 003 va endpoint /memberships; paymentStatus suy tu danh sach da thanh toan.
    private static Membership360 ToMembership360(Membership membership, HashSet<long> paidIds)
    {
        return new Membership360(
            membership.Id,
            membership.PackageId,
            membership.Package.Name,
            membership.Package.SupportsPT,
            membership.StartDate,
            membership.EndDate,
            membership.Status.ToString(),
            paidIds.Contains(membership.Id) ? PaymentStatus.Paid.ToString() : PaymentStatus.Pending.ToString());
    }

    private Task<MemberProfile?> FindMemberAsync(long memberId, CancellationToken cancellationToken)
    {
        return _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted, cancellationToken);
    }

    private async Task<bool> CanAccessAsync(
        ClaimsPrincipal principal,
        MemberProfile profile,
        CancellationToken cancellationToken)
    {
        if (principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.Staff))
        {
            return true;
        }

        if (principal.IsInRole(RoleNames.Member))
        {
            return GetActorId(principal) == profile.UserId;
        }

        // PT chi xem/ghi duoc hoi vien dang duoc phan cong active cho minh (giong WorkoutPlanService).
        if (principal.IsInRole(RoleNames.Pt))
        {
            var actorId = GetActorId(principal);
            if (actorId is null)
            {
                return false;
            }

            var trainerProfileId = await _dbContext.TrainerProfiles
                .Where(item => item.UserId == actorId && !item.IsDeleted)
                .Select(item => (long?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (trainerProfileId is null)
            {
                return false;
            }

            return await _dbContext.TrainerAssignments.AnyAsync(
                item => item.TrainerId == trainerProfileId
                    && item.MemberId == profile.Id
                    && item.Status == AssignmentStatuses.Active,
                cancellationToken);
        }

        return false;
    }

    private static bool HasAnyMeasurement(RecordProgressRequest request)
    {
        return request.WeightKg is not null ||
            request.BodyFatPct is not null ||
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

    private static bool ExpireStalePending(IEnumerable<Membership> memberships)
    {
        var changed = false;
        var cutoff = DateTime.UtcNow - PendingPaymentTtl;
        foreach (var membership in memberships.Where(item => item.Status == MembershipStatus.PendingPayment && item.CreatedAt < cutoff))
        {
            membership.Status = MembershipStatus.Cancelled;
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
