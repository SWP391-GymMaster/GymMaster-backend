using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly GymMasterDbContext _db;

    // Suc chua toi da cua phong gym (dung tinh % tai van hanh)
    private const int GymCapacity = 50;

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
        var now = DateTime.UtcNow;
        var fromUtc = from?.ToUniversalTime() ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = to?.ToUniversalTime() ?? now;

        if (fromUtc > toUtc)
        {
            return AuthServiceResult<DashboardSummaryResponse>.Failure(
                "INVALID_RANGE",
                "Khoang ngay khong hop le: from > to.",
                StatusCodes.Status422UnprocessableEntity);
        }

        var todayDate = DateOnly.FromDateTime(now);
        var todayUtcStart = now.Date;
        var todayUtcEnd = todayUtcStart.AddDays(1);

        // --- Tong doanh thu trong khoang ngay ---
        var revenue = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Paid
                     && p.PaidAt.HasValue
                     && p.PaidAt.Value >= fromUtc
                     && p.PaidAt.Value <= toUtc)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // --- So membership Active tai thoi diem hien tai ---
        var activeCount = await _db.Memberships
            .CountAsync(m => m.Status == MembershipStatus.Active && m.EndDate >= todayDate, cancellationToken);

        // --- So membership Expired ---
        var expiredCount = await _db.Memberships
            .CountAsync(m => m.Status == MembershipStatus.Expired
                          || (m.Status == MembershipStatus.Active && m.EndDate < todayDate),
                        cancellationToken);

        // --- Check-in hom nay (luon tinh rieng, doc lap voi khoang ngay doanh thu) ---
        var todayCheckInCount = await _db.CheckIns
            .CountAsync(c => c.CheckInAt >= todayUtcStart && c.CheckInAt < todayUtcEnd, cancellationToken);
        var checkinsResult = new List<CheckInByDayItem> { new(todayDate, todayCheckInCount) };

        // --- Thanh toan cho xu ly ---
        var pendingPaymentAmount = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var pendingPaymentCount = await _db.Payments
            .CountAsync(p => p.Status == PaymentStatus.Pending, cancellationToken);

        // --- Doanh thu theo thang (6 thang gan nhat) ---
        var sixMonthsAgoStart = new DateTime(
            now.AddMonths(-5).Year,
            now.AddMonths(-5).Month,
            1, 0, 0, 0, DateTimeKind.Utc);

        var revenueByMonthRaw = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Paid
                     && p.PaidAt.HasValue
                     && p.PaidAt.Value >= sixMonthsAgoStart)
            .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);

        var revenueByMonth = new List<RevenueByMonthItem>();
        for (int i = -5; i <= 0; i++)
        {
            var d = now.AddMonths(i);
            var found = revenueByMonthRaw.FirstOrDefault(r => r.Year == d.Year && r.Month == d.Month);
            revenueByMonth.Add(new RevenueByMonthItem($"T{d.Month}", found?.Revenue ?? 0m));
        }

        // --- Hoi vien het han gan day (top 10) ---
        var expiredRaw = await _db.Memberships
            .Where(m => m.Status == MembershipStatus.Expired
                     || (m.Status == MembershipStatus.Active && m.EndDate < todayDate))
            .OrderByDescending(m => m.EndDate)
            .Take(10)
            .Select(m => new
            {
                m.EndDate,
                FullName = m.Member.User.FullName,
                PackageName = m.Package.Name,
            })
            .ToListAsync(cancellationToken);

        var recentlyExpired = expiredRaw
            .Select(m => new ExpiredMembershipItem(
                GetInitials(m.FullName),
                m.FullName,
                m.PackageName,
                m.EndDate))
            .ToList();

        // --- Tai van hanh co so: check-in hom nay / suc chua phong ---
        var ptCheckInsToday = await _db.CheckIns
            .Where(c => c.CheckInAt >= todayUtcStart && c.CheckInAt < todayUtcEnd
                     && _db.TrainerAssignments.Any(a => a.MemberId == c.MemberId && a.Status == AssignmentStatuses.Active))
            .CountAsync(cancellationToken);

        var facilityLoadPercent = Math.Min(100, (int)Math.Round((double)todayCheckInCount / GymCapacity * 100));
        var ptSessionPercent = Math.Min(facilityLoadPercent, (int)Math.Round((double)ptCheckInsToday / GymCapacity * 100));
        var generalAreaPercent = Math.Max(0, facilityLoadPercent - ptSessionPercent);

        // --- Doanh thu thang truoc (de tinh % tang/giam) ---
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = thisMonthStart.AddMonths(-1);
        var previousMonthRevenue = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Paid
                     && p.PaidAt.HasValue
                     && p.PaidAt.Value >= prevMonthStart
                     && p.PaidAt.Value < thisMonthStart)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // --- So membership moi trong thang nay ---
        var thisMonthStartDate = DateOnly.FromDateTime(thisMonthStart);
        var newMembershipsThisMonth = await _db.Memberships
            .CountAsync(m => m.StartDate >= thisMonthStartDate, cancellationToken);

        // --- Gio cao diem check-in (UTC+7, 30 ngay gan nhat) ---
        var thirtyDaysAgo = now.AddDays(-30);
        var checkInTimes = await _db.CheckIns
            .Where(c => c.CheckInAt >= thirtyDaysAgo)
            .Select(c => c.CheckInAt)
            .ToListAsync(cancellationToken);

        int peakHourStart = 17;
        int peakHourEnd = 19;
        if (checkInTimes.Count > 0)
        {
            var peak = checkInTimes
                .GroupBy(dt => (dt.Hour + 7) % 24)
                .OrderByDescending(g => g.Count())
                .First();
            peakHourStart = peak.Key;
            peakHourEnd = (peakHourStart + 2) % 24;
        }

        var summary = new DashboardSummaryResponse(
            revenue,
            activeCount,
            expiredCount,
            checkinsResult,
            pendingPaymentAmount,
            pendingPaymentCount,
            revenueByMonth,
            recentlyExpired,
            facilityLoadPercent,
            ptSessionPercent,
            generalAreaPercent,
            previousMonthRevenue,
            newMembershipsThisMonth,
            peakHourStart,
            peakHourEnd);

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

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
