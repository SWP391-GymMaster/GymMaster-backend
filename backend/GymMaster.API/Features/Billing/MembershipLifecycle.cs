using GymMaster.API.Entities;

namespace GymMaster.API.Features.Billing;
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

    // Noi han khi kich hoat mot don da thanh toan (BIZ-01: toi da 1 Active/member).
    // Con goi Active khac -> EndDate moi = EndDate cu + DurationDays, goi cu chuyen Cancelled.
    // Khong con -> tinh tu hom nay. Tra ve goi Active bi thay the (null neu khong co).
    //
    // Dung CHUNG cho ca hai duong kich hoat: thu tien tay (MembershipService.ConfirmPaymentAsync)
    // va thanh toan online (VnPayService.FinalizeSuccessfulPaymentAsync) — hai luong PHAI
    // cho ra cung ket qua, nen luat chi duoc ton tai o day.
    public static Membership? ApplyPaidRenewalWindow(
        Membership membership,
        IEnumerable<Membership> otherMemberships,
        DateOnly today)
    {
        var now = DateTime.UtcNow;
        var activeMembership = otherMemberships.FirstOrDefault(item => IsActiveOn(item, today));

        membership.EndDate = activeMembership is null
            ? today.AddDays(membership.Package.DurationDays)
            : activeMembership.EndDate.AddDays(membership.Package.DurationDays);

        membership.Status = MembershipStatus.Active;
        membership.UpdatedAt = now;

        if (activeMembership is not null)
        {
            activeMembership.Status = MembershipStatus.Cancelled;
            activeMembership.UpdatedAt = now;
        }

        return activeMembership;
    }
}
