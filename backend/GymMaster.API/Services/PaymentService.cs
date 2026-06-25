using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class PaymentService : IPaymentService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly GymMasterDbContext _dbContext;

    public PaymentService(GymMasterDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthServiceResult<PagedResult<PaymentHistoryResponse>>> GetAllAsync(
        DateOnly? from,
        DateOnly? to,
        string? status,
        long? memberId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var validation = ValidateDateRange(from, to);
        if (validation is not null)
        {
            return Fail<PagedResult<PaymentHistoryResponse>>(validation.Value.code, validation.Value.message);
        }

        PaymentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PaymentStatus>(status, ignoreCase: true, out var parsed))
            {
                return Fail<PagedResult<PaymentHistoryResponse>>(
                    "VALIDATION_ERROR",
                    "Trang thai payment khong hop le.");
            }

            statusFilter = parsed;
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var query = BuildPaymentQuery(from, to);

        if (statusFilter is not null)
        {
            query = query.Where(item => item.Status == statusFilter);
        }

        if (memberId is not null)
        {
            query = query.Where(item => item.MemberId == memberId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.PaymentDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var result = new PagedResult<PaymentHistoryResponse>(
            rows.Select(ToResponse).ToList(),
            page,
            pageSize,
            totalItems,
            totalPages);

        return AuthServiceResult<PagedResult<PaymentHistoryResponse>>.Success(result);
    }

    public async Task<AuthServiceResult<IReadOnlyList<PaymentHistoryResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.MemberProfiles
            .Include(item => item.User)
            .FirstOrDefaultAsync(
                item => item.Id == memberId && !item.IsDeleted && !item.User.IsDeleted,
                cancellationToken);

        if (profile is null)
        {
            return Fail<IReadOnlyList<PaymentHistoryResponse>>(
                "NOT_FOUND",
                "Khong tim thay hoi vien.",
                StatusCodes.Status404NotFound);
        }

        if (!CanAccess(principal, profile))
        {
            return Fail<IReadOnlyList<PaymentHistoryResponse>>(
                "FORBIDDEN",
                "Ban khong co quyen xem lich su thanh toan nay.",
                StatusCodes.Status403Forbidden);
        }

        var rows = await BuildPaymentQuery(null, null)
            .Where(item => item.MemberId == memberId)
            .OrderByDescending(item => item.PaymentDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return AuthServiceResult<IReadOnlyList<PaymentHistoryResponse>>.Success(
            rows.Select(ToResponse).ToList());
    }

    public async Task<AuthServiceResult<PaymentSummaryResponse>> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var validation = ValidateDateRange(from, to);
        if (validation is not null)
        {
            return Fail<PaymentSummaryResponse>(validation.Value.code, validation.Value.message);
        }

        var rows = await BuildPaymentQuery(from, to).ToListAsync(cancellationToken);
        var paidRows = rows.Where(item => item.Status == PaymentStatus.Paid).ToList();

        var revenue = paidRows.Sum(item => item.Amount);

        var byMethod = paidRows
            .GroupBy(item => item.PaymentMethod)
            .OrderBy(item => item.Key.ToString())
            .Select(item => new PaymentMethodSummaryResponse(
                item.Key.ToString(),
                item.Count(),
                item.Sum(payment => payment.Amount)))
            .ToList();

        var byDay = paidRows
            .GroupBy(item => DateOnly.FromDateTime(item.PaymentDate))
            .OrderBy(item => item.Key)
            .Select(item => new DailyRevenueResponse(
                item.Key,
                item.Count(),
                item.Sum(payment => payment.Amount)))
            .ToList();

        var result = new PaymentSummaryResponse(
            from,
            to,
            rows.Count,
            paidRows.Count,
            rows.Count(item => item.Status == PaymentStatus.Pending),
            revenue,
            byMethod,
            byDay);

        return AuthServiceResult<PaymentSummaryResponse>.Success(result);
    }

    private IQueryable<PaymentQueryRow> BuildPaymentQuery(DateOnly? from, DateOnly? to)
    {
        var query =
            from payment in _dbContext.Payments
            join membership in _dbContext.Memberships on payment.MembershipId equals membership.Id
            join memberProfile in _dbContext.MemberProfiles on membership.MemberId equals memberProfile.Id
            join memberUser in _dbContext.Users on memberProfile.UserId equals memberUser.Id
            join package in _dbContext.MembershipPackages on membership.PackageId equals package.Id
            join creatorUser in _dbContext.Users on payment.CreatedByUserId equals creatorUser.Id into creators
            from creator in creators.DefaultIfEmpty()
            where !memberProfile.IsDeleted && !memberUser.IsDeleted
            select new PaymentQueryRow
            {
                Id = payment.Id,
                MembershipId = payment.MembershipId,
                MemberId = membership.MemberId,
                MemberName = memberUser.FullName,
                MemberEmail = memberUser.Email,
                PackageId = package.Id,
                PackageName = package.Name,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                Status = payment.Status,
                MembershipStatus = membership.Status,
                PaymentDate = payment.PaidAt ?? payment.CreatedAt,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt,
                CreatedByUserId = payment.CreatedByUserId,
                CreatedByName = creator == null ? null : creator.FullName
            };

        if (from is not null)
        {
            var fromDate = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.PaymentDate >= fromDate);
        }

        if (to is not null)
        {
            var toExclusive = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.PaymentDate < toExclusive);
        }

        return query;
    }

    private static PaymentHistoryResponse ToResponse(PaymentQueryRow row)
    {
        return new PaymentHistoryResponse(
            row.Id,
            row.MembershipId,
            row.MemberId,
            row.MemberName,
            row.MemberEmail,
            row.PackageId,
            row.PackageName,
            row.Amount,
            row.PaymentMethod.ToString(),
            row.Status.ToString(),
            row.MembershipStatus.ToString(),
            row.PaymentDate,
            row.PaidAt,
            row.CreatedAt,
            row.CreatedByUserId,
            row.CreatedByName);
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

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static (string code, string message)? ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is not null && to is not null && from > to)
        {
            return ("VALIDATION_ERROR", "Khoang ngay khong hop le.");
        }

        return null;
    }

    private static AuthServiceResult<T> Fail<T>(
        string code,
        string message,
        int statusCode = StatusCodes.Status422UnprocessableEntity)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }

    private sealed class PaymentQueryRow
    {
        public long Id { get; set; }

        public long MembershipId { get; set; }

        public long MemberId { get; set; }

        public string MemberName { get; set; } = string.Empty;

        public string MemberEmail { get; set; } = string.Empty;

        public long PackageId { get; set; }

        public string PackageName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public PaymentStatus Status { get; set; }

        public MembershipStatus MembershipStatus { get; set; }

        public DateTime PaymentDate { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public long CreatedByUserId { get; set; }

        public string? CreatedByName { get; set; }
    }
}
