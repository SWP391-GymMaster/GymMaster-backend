using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class MembershipService : IMembershipService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public MembershipService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-MS-01
    public async Task<AuthServiceResult<MembershipResponse>> SellAsync(
        SellMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null)
        {
            return Fail<MembershipResponse>("FORBIDDEN", "Khong xac dinh duoc nguoi thao tac.", StatusCodes.Status403Forbidden);
        }

        var memberExists = await _dbContext.MemberProfiles.AnyAsync(
            item => item.Id == request.MemberId && !item.IsDeleted, cancellationToken);

        if (!memberExists)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        var package = await _dbContext.MembershipPackages.FirstOrDefaultAsync(
            item => item.Id == request.PackageId, cancellationToken);

        if (package is null)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay goi tap.", StatusCodes.Status404NotFound);
        }

        if (!package.IsActive)
        {
            return Fail<MembershipResponse>("PACKAGE_INACTIVE", "Goi tap da ngung ban.", StatusCodes.Status422UnprocessableEntity);
        }

        var startDate = request.StartDate ?? Today();
        var membership = new Membership
        {
            MemberId = request.MemberId,
            PackageId = package.Id,
            Package = package,
            StartDate = startDate,
            EndDate = startDate.AddDays(package.DurationDays),
            Status = MembershipStatus.PendingPayment,
            CreatedByUserId = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "SELL_MEMBERSHIP",
            "Membership",
            membership.Id,
            new { membership.MemberId, membership.PackageId },
            cancellationToken);

        return AuthServiceResult<MembershipResponse>.Success(
            ToResponse(membership),
            StatusCodes.Status201Created);
    }

    // FR-MS-02 / FR-PAY-02 / FR-MS-08
    public async Task<AuthServiceResult<MembershipResponse>> ConfirmPaymentAsync(
        long id,
        ConfirmPaymentRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null)
        {
            return Fail<MembershipResponse>("FORBIDDEN", "Khong xac dinh duoc nguoi thao tac.", StatusCodes.Status403Forbidden);
        }

        var membership = await _dbContext.Memberships
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (membership is null)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay membership.", StatusCodes.Status404NotFound);
        }

        if (membership.Status != MembershipStatus.PendingPayment)
        {
            return Fail<MembershipResponse>("ALREADY_PAID", "Membership nay da duoc thanh toan.", StatusCodes.Status409Conflict);
        }

        if (request.Amount <= 0 || !Enum.IsDefined(typeof(PaymentMethod), request.Method))
        {
            return Fail<MembershipResponse>("VALIDATION_ERROR", "Thong tin thanh toan khong hop le.", StatusCodes.Status422UnprocessableEntity);
        }

        var today = Today();
        var otherMemberships = await _dbContext.Memberships
            .Where(item => item.MemberId == membership.MemberId && item.Id != membership.Id)
            .ToListAsync(cancellationToken);

        ExpireIfPastDue(otherMemberships, today);

        if (otherMemberships.Any(item => item.Status == MembershipStatus.Active && item.EndDate >= today))
        {
            return Fail<MembershipResponse>(
                "ALREADY_HAS_ACTIVE",
                "Hoi vien da co membership dang hieu luc.",
                StatusCodes.Status409Conflict);
        }

        var payment = new Payment
        {
            MembershipId = membership.Id,
            Amount = request.Amount,
            PaymentMethod = (PaymentMethod)request.Method,
            Status = PaymentStatus.Paid,
            PaidAt = DateTime.UtcNow,
            CreatedByUserId = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);
        membership.Status = MembershipStatus.Active;
        membership.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "CONFIRM_PAYMENT",
            "Payment",
            payment.Id,
            new { membershipId = membership.Id },
            cancellationToken);

        return AuthServiceResult<MembershipResponse>.Success(
            ToResponse(membership),
            StatusCodes.Status201Created);
    }

    // FR-MS-03
    public async Task<AuthServiceResult<MembershipResponse>> RenewAsync(
        long id,
        RenewMembershipRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null)
        {
            return Fail<MembershipResponse>("FORBIDDEN", "Khong xac dinh duoc nguoi thao tac.", StatusCodes.Status403Forbidden);
        }

        var membership = await _dbContext.Memberships
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (membership is null)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay membership.", StatusCodes.Status404NotFound);
        }

        var package = await _dbContext.MembershipPackages.FirstOrDefaultAsync(
            item => item.Id == request.PackageId, cancellationToken);

        if (package is null)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay goi tap.", StatusCodes.Status404NotFound);
        }

        if (!package.IsActive)
        {
            return Fail<MembershipResponse>("PACKAGE_INACTIVE", "Goi tap da ngung ban.", StatusCodes.Status422UnprocessableEntity);
        }

        var today = Today();
        if (membership.PackageId == package.Id &&
            membership.Status == MembershipStatus.Active &&
            membership.EndDate >= today)
        {
            membership.EndDate = membership.EndDate.AddDays(package.DurationDays);
            membership.UpdatedAt = DateTime.UtcNow;

            _dbContext.Payments.Add(new Payment
            {
                MembershipId = membership.Id,
                Amount = package.Price,
                PaymentMethod = PaymentMethod.Cash,
                Status = PaymentStatus.Paid,
                PaidAt = DateTime.UtcNow,
                CreatedByUserId = actorId.Value,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                "RENEW_MEMBERSHIP",
                "Membership",
                membership.Id,
                new { extendedTo = membership.EndDate },
                cancellationToken);

            return AuthServiceResult<MembershipResponse>.Success(ToResponse(membership));
        }

        var newMembership = new Membership
        {
            MemberId = membership.MemberId,
            PackageId = package.Id,
            Package = package,
            StartDate = today,
            EndDate = today.AddDays(package.DurationDays),
            Status = MembershipStatus.PendingPayment,
            CreatedByUserId = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Memberships.Add(newMembership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "RENEW_MEMBERSHIP",
            "Membership",
            newMembership.Id,
            new { sourceMembershipId = membership.Id },
            cancellationToken);

        return AuthServiceResult<MembershipResponse>.Success(
            ToResponse(newMembership),
            StatusCodes.Status201Created);
    }

    // FR-MS-04
    public async Task<AuthServiceResult<MembershipResponse>> CreateRenewalRequestAsync(
        RenewalRequestRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null)
        {
            return Fail<MembershipResponse>("FORBIDDEN", "Khong xac dinh duoc nguoi thao tac.", StatusCodes.Status403Forbidden);
        }

        var profile = await _dbContext.MemberProfiles.FirstOrDefaultAsync(
            item => item.UserId == actorId.Value && !item.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return Fail<MembershipResponse>("FORBIDDEN", "Ban khong co quyen tao yeu cau gia han.", StatusCodes.Status403Forbidden);
        }

        var package = await _dbContext.MembershipPackages.FirstOrDefaultAsync(
            item => item.Id == request.PackageId, cancellationToken);

        if (package is null)
        {
            return Fail<MembershipResponse>("NOT_FOUND", "Khong tim thay goi tap.", StatusCodes.Status404NotFound);
        }

        if (!package.IsActive)
        {
            return Fail<MembershipResponse>("PACKAGE_INACTIVE", "Goi tap da ngung ban.", StatusCodes.Status422UnprocessableEntity);
        }

        var today = Today();
        var membership = new Membership
        {
            MemberId = profile.Id,
            PackageId = package.Id,
            Package = package,
            StartDate = today,
            EndDate = today.AddDays(package.DurationDays),
            Status = MembershipStatus.PendingPayment,
            CreatedByUserId = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("REQUEST_RENEWAL", "Membership", membership.Id, null, cancellationToken);

        return AuthServiceResult<MembershipResponse>.Success(
            ToResponse(membership),
            StatusCodes.Status201Created);
    }

    // FR-MS-09
    public async Task<AuthServiceResult<IReadOnlyList<MembershipResponse>>> GetMembershipsForMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<MembershipResponse>>(
                "NOT_FOUND",
                "Khong tim thay hoi vien.",
                StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<IReadOnlyList<MembershipResponse>>(
                "FORBIDDEN",
                "Ban khong co quyen xem membership nay.",
                StatusCodes.Status403Forbidden);
        }

        var memberships = await _dbContext.Memberships
            .Include(item => item.Package)
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        ExpireIfPastDue(memberships, Today());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<MembershipResponse>>.Success(
            memberships.Select(ToResponse).ToList());
    }

    // 🆕 API_CONTRACT_Y: roster tat ca membership cho Admin/Staff.
    public async Task<AuthServiceResult<IReadOnlyList<MembershipResponse>>> GetAllAsync(
        string? status,
        int? page,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        MembershipStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<MembershipStatus>(status, ignoreCase: true, out var parsed))
            {
                return Fail<IReadOnlyList<MembershipResponse>>(
                    "VALIDATION_ERROR",
                    "Trang thai membership khong hop le.",
                    StatusCodes.Status422UnprocessableEntity);
            }

            statusFilter = parsed;
        }

        var today = Today();

        // Het han truoc khi tra ve de trang thai chinh xac (giong GetMembershipsForMemberAsync).
        var pastDue = await _dbContext.Memberships
            .Where(item => item.Status == MembershipStatus.Active && item.EndDate < today)
            .ToListAsync(cancellationToken);

        if (pastDue.Count > 0)
        {
            ExpireIfPastDue(pastDue, today);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var query = _dbContext.Memberships
            .Include(item => item.Package)
            .AsQueryable();

        if (statusFilter is not null)
        {
            query = query.Where(item => item.Status == statusFilter);
        }

        query = query.OrderByDescending(item => item.CreatedAt);

        if (page is int pageNumber && pageNumber > 0)
        {
            const int pageSize = 50;
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
        }

        var memberships = await query.ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<MembershipResponse>>.Success(
            memberships.Select(ToResponse).ToList());
    }

    private static void ExpireIfPastDue(IEnumerable<Membership> memberships, DateOnly today)
    {
        foreach (var membership in memberships.Where(item => item.Status == MembershipStatus.Active && item.EndDate < today))
        {
            membership.Status = MembershipStatus.Expired;
            membership.UpdatedAt = DateTime.UtcNow;
        }
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

    private static MembershipResponse ToResponse(Membership membership)
    {
        var today = Today();
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

    private static DateOnly Today()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow);
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
