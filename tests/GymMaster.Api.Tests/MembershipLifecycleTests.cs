using GymMaster.API.Entities;
using GymMaster.API.Features.Billing;
using Xunit;

namespace GymMaster.Api.Tests;

// MembershipLifecycle la logic THUAN (khong I/O) nhung duoc 7 feature dung chung
// (003 · 004 · 005 · 006 · 007 · 009 · 010) — test o day bao ve nhieu nhat tren
// mot don vi cong suc. Khong can DbContext.
public class MembershipLifecycleTests
{
    private const short Days = 30;

    private static Membership NewMembership(
        MembershipStatus status,
        DateOnly endDate,
        DateTime? createdAt = null,
        long id = 1)
    {
        return new Membership
        {
            Id = id,
            MemberId = 1,
            PackageId = 1,
            Package = new MembershipPackage { Id = 1, DurationDays = Days, Price = 500_000m },
            StartDate = endDate.AddDays(-Days),
            EndDate = endDate,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    // ---- IsActiveOn: dinh nghia DUY NHAT cua "goi dang hieu luc" (BIZ-04) ----

    [Fact]
    public void IsActiveOn_true_when_active_and_not_expired()
    {
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(MembershipStatus.Active, today.AddDays(5));

        Assert.True(MembershipLifecycle.IsActiveOn(membership, today));
    }

    [Fact]
    public void IsActiveOn_true_on_the_last_day_boundary()
    {
        // EndDate == hom nay van con hieu luc (>=, khong phai >).
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(MembershipStatus.Active, today);

        Assert.True(MembershipLifecycle.IsActiveOn(membership, today));
    }

    [Fact]
    public void IsActiveOn_false_when_past_end_date()
    {
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(MembershipStatus.Active, today.AddDays(-1));

        Assert.False(MembershipLifecycle.IsActiveOn(membership, today));
    }

    [Theory]
    [InlineData(MembershipStatus.PendingPayment)]
    [InlineData(MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Cancelled)]
    public void IsActiveOn_false_for_every_non_active_status(MembershipStatus status)
    {
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(status, today.AddDays(30));

        Assert.False(MembershipLifecycle.IsActiveOn(membership, today));
    }

    // ---- ExpireIfPastDue: lazy expire (GBL-10 — khong co job chay nen) ----

    [Fact]
    public void ExpireIfPastDue_moves_past_due_active_to_expired()
    {
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(MembershipStatus.Active, today.AddDays(-1));

        var changed = MembershipLifecycle.ExpireIfPastDue(new[] { membership }, today);

        Assert.True(changed);
        Assert.Equal(MembershipStatus.Expired, membership.Status);
    }

    [Fact]
    public void ExpireIfPastDue_leaves_membership_ending_today_untouched()
    {
        var today = new DateOnly(2026, 7, 1);
        var membership = NewMembership(MembershipStatus.Active, today);

        var changed = MembershipLifecycle.ExpireIfPastDue(new[] { membership }, today);

        Assert.False(changed);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void ExpireIfPastDue_does_not_touch_pending_or_cancelled()
    {
        var today = new DateOnly(2026, 7, 1);
        var pending = NewMembership(MembershipStatus.PendingPayment, today.AddDays(-10), id: 1);
        var cancelled = NewMembership(MembershipStatus.Cancelled, today.AddDays(-10), id: 2);

        var changed = MembershipLifecycle.ExpireIfPastDue(new[] { pending, cancelled }, today);

        Assert.False(changed);
        Assert.Equal(MembershipStatus.PendingPayment, pending.Status);
        Assert.Equal(MembershipStatus.Cancelled, cancelled.Status);
    }

    // ---- ExpireStalePending: TTL 30 phut (BIZ-03, spec 003 AC-09) ----

    [Fact]
    public void PendingPaymentTtl_is_thirty_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), MembershipLifecycle.PendingPaymentTtl);
    }

    [Fact]
    public void ExpireStalePending_cancels_order_older_than_ttl()
    {
        var stale = NewMembership(
            MembershipStatus.PendingPayment,
            new DateOnly(2026, 8, 1),
            createdAt: DateTime.UtcNow.AddMinutes(-31));

        var changed = MembershipLifecycle.ExpireStalePending(new[] { stale });

        Assert.True(changed);
        Assert.Equal(MembershipStatus.Cancelled, stale.Status);
    }

    [Fact]
    public void ExpireStalePending_keeps_order_still_inside_ttl()
    {
        var fresh = NewMembership(
            MembershipStatus.PendingPayment,
            new DateOnly(2026, 8, 1),
            createdAt: DateTime.UtcNow.AddMinutes(-29));

        var changed = MembershipLifecycle.ExpireStalePending(new[] { fresh });

        Assert.False(changed);
        Assert.Equal(MembershipStatus.PendingPayment, fresh.Status);
    }

    [Fact]
    public void ExpireStalePending_ignores_active_membership_however_old()
    {
        var oldActive = NewMembership(
            MembershipStatus.Active,
            new DateOnly(2026, 8, 1),
            createdAt: DateTime.UtcNow.AddDays(-90));

        var changed = MembershipLifecycle.ExpireStalePending(new[] { oldActive });

        Assert.False(changed);
        Assert.Equal(MembershipStatus.Active, oldActive.Status);
    }

    // Hai ham nay duoc goi CUNG NHAU bang toan tu `|` (khong phai `||`) o
    // ProgressService/MembershipService. Neu ai do doi sang `||` thi
    // ExpireStalePending se bi short-circuit khi ExpireIfPastDue tra true.
    [Fact]
    public void Both_expiry_rules_apply_in_the_same_pass()
    {
        var today = new DateOnly(2026, 7, 1);
        var pastDue = NewMembership(MembershipStatus.Active, today.AddDays(-1), id: 1);
        var stalePending = NewMembership(
            MembershipStatus.PendingPayment,
            today.AddDays(30),
            createdAt: DateTime.UtcNow.AddMinutes(-31),
            id: 2);
        var all = new[] { pastDue, stalePending };

        var changed = MembershipLifecycle.ExpireIfPastDue(all, today) | MembershipLifecycle.ExpireStalePending(all);

        Assert.True(changed);
        Assert.Equal(MembershipStatus.Expired, pastDue.Status);
        Assert.Equal(MembershipStatus.Cancelled, stalePending.Status);
    }

    // ---- ApplyPaidRenewalWindow: luat noi han (BIZ-01) ----
    // Gom ve mot nguon (B-20) — truoc day MembershipService va VnPayService moi ben
    // giu mot ban sao. Test o day dam bao CA HAI luong kich hoat cho ket qua giong nhau.

    [Fact]
    public void ApplyPaidRenewalWindow_counts_from_today_when_no_active_package()
    {
        var today = new DateOnly(2026, 7, 1);
        var pending = NewMembership(MembershipStatus.PendingPayment, today, id: 1);

        var replaced = MembershipLifecycle.ApplyPaidRenewalWindow(pending, Array.Empty<Membership>(), today);

        Assert.Null(replaced);
        Assert.Equal(MembershipStatus.Active, pending.Status);
        Assert.Equal(today.AddDays(Days), pending.EndDate);
    }

    [Fact]
    public void ApplyPaidRenewalWindow_chains_onto_the_existing_active_package()
    {
        var today = new DateOnly(2026, 7, 1);
        var currentEnd = today.AddDays(10);
        var active = NewMembership(MembershipStatus.Active, currentEnd, id: 1);
        var pending = NewMembership(MembershipStatus.PendingPayment, today, id: 2);

        var replaced = MembershipLifecycle.ApplyPaidRenewalWindow(pending, new[] { active }, today);

        // Noi tiep: khach khong mat 10 ngay con lai.
        Assert.Equal(currentEnd.AddDays(Days), pending.EndDate);
        Assert.Equal(MembershipStatus.Active, pending.Status);
        // Giu bat bien BIZ-01: toi da 1 Active/member.
        Assert.Same(active, replaced);
        Assert.Equal(MembershipStatus.Cancelled, active.Status);
    }

    [Fact]
    public void ApplyPaidRenewalWindow_ignores_expired_package_and_counts_from_today()
    {
        var today = new DateOnly(2026, 7, 1);
        var expired = NewMembership(MembershipStatus.Active, today.AddDays(-5), id: 1);
        var pending = NewMembership(MembershipStatus.PendingPayment, today, id: 2);

        var replaced = MembershipLifecycle.ApplyPaidRenewalWindow(pending, new[] { expired }, today);

        Assert.Null(replaced);
        Assert.Equal(today.AddDays(Days), pending.EndDate);
        // Goi da het han khong bi dong thanh Cancelled o buoc nay.
        Assert.Equal(MembershipStatus.Active, expired.Status);
    }

    [Fact]
    public void ApplyPaidRenewalWindow_ignores_cancelled_package()
    {
        var today = new DateOnly(2026, 7, 1);
        var cancelled = NewMembership(MembershipStatus.Cancelled, today.AddDays(20), id: 1);
        var pending = NewMembership(MembershipStatus.PendingPayment, today, id: 2);

        var replaced = MembershipLifecycle.ApplyPaidRenewalWindow(pending, new[] { cancelled }, today);

        Assert.Null(replaced);
        Assert.Equal(today.AddDays(Days), pending.EndDate);
    }
}
