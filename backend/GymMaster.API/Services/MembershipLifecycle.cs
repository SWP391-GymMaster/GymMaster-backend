using GymMaster.API.Entities;

namespace GymMaster.API.Services;

// Luat vong doi membership dung CHUNG cho moi service (FR-MS-07).
// Truoc day 3 ban sao nam o MembershipService/ProgressService/VnPayService —
// gom ve 1 noi de moi duong doc/ghi phan xet trang thai GIONG HET nhau.
internal static class MembershipLifecycle
{
    // Don PendingPayment qua 30 phut chua thanh toan -> coi nhu bo, tu huy.
    public static readonly TimeSpan PendingPaymentTtl = TimeSpan.FromMinutes(30);

    // Dinh nghia duy nhat cua "goi dang hoat dong": Active VA con han.
    public static bool IsActiveOn(Membership membership, DateOnly today)
    {
        return membership.Status == MembershipStatus.Active && membership.EndDate >= today;
    }

    // Lazy-expire: Active da qua EndDate -> Expired. Tra ve true neu co thay doi
    // de caller quyet dinh co SaveChanges hay khong.
    public static bool ExpireIfPastDue(IEnumerable<Membership> memberships, DateOnly today)
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

    // Don cho thanh toan qua TTL -> Cancelled (don rac khong ai quay lai tra tien).
    public static bool ExpireStalePending(IEnumerable<Membership> memberships)
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
}
