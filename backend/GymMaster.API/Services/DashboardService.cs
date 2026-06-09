using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly GymMasterDbContext _db;

    public DashboardService(GymMasterDbContext db)
    {
        _db = db;
    }

    // FR-DASH-01/02/03
    public async Task<AuthServiceResult<DashboardSummaryResponse>> GetSummaryAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = to?.ToUniversalTime() ?? DateTime.UtcNow;

        if (fromUtc > toUtc)
        {
            return AuthServiceResult<DashboardSummaryResponse>.Failure(
                "INVALID_RANGE",
                "Khoang ngay khong hop le: from > to.",
                StatusCodes.Status422UnprocessableEntity);
        }

        // Tong doanh thu: sum(payments.amount) where status=Paid and paidAt in range
        var revenue = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Paid
                     && p.PaidAt.HasValue
                     && p.PaidAt.Value >= fromUtc
                     && p.PaidAt.Value <= toUtc)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // So membership Active tai thoi diem hien tai (khong phu thuoc khoang ngay)
        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeCount = await _db.Memberships
            .CountAsync(m => m.Status == MembershipStatus.Active && m.EndDate >= todayDate, cancellationToken);

        // So membership Expired (het han)
        var expiredCount = await _db.Memberships
            .CountAsync(m => m.Status == MembershipStatus.Expired
                          || (m.Status == MembershipStatus.Active && m.EndDate < todayDate),
                        cancellationToken);

        // Check-in theo ngay trong khoang
        var checkinsByDay = await _db.CheckIns
            .Where(c => c.CheckInAt >= fromUtc && c.CheckInAt <= toUtc)
            .GroupBy(c => new { c.CheckInAt.Year, c.CheckInAt.Month, c.CheckInAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Count = g.Count()
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
            .ToListAsync(cancellationToken);

        var checkinsResult = checkinsByDay
            .Select(g => new CheckInByDayItem(new DateOnly(g.Year, g.Month, g.Day), g.Count))
            .ToList();

        var summary = new DashboardSummaryResponse(revenue, activeCount, expiredCount, checkinsResult);
        return AuthServiceResult<DashboardSummaryResponse>.Success(summary);
    }

    // FR-AUD-02
    public async Task<AuthServiceResult<PagedResult<AuditLogResponse>>> GetAuditLogsAsync(
        long? userId,
        string? action,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AuditLogs.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(a => a.Action.Contains(keyword) || a.Entity.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);

        // Lay danh sach audit log truoc, sau do resolve ten nguoi dung (left join thu cong de xu ly UserId null)
        var rawItems = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = rawItems.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var responses = rawItems.Select(a => new AuditLogResponse(
            a.Id,
            a.UserId,
            a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var name) ? name : null,
            a.Action,
            a.Entity,
            a.EntityId,
            a.Metadata,
            a.CreatedAt)).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var result = new PagedResult<AuditLogResponse>(responses, page, pageSize, total, totalPages);
        return AuthServiceResult<PagedResult<AuditLogResponse>>.Success(result);
    }
}
