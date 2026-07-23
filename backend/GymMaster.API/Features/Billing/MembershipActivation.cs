using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Features.Billing;

// Cac buoc GHI khi kich hoat mot membership da thanh toan.
// Tach rieng khoi MembershipLifecycle (thuan, khong I/O) vi cac ham o day cham DbContext.
//
// Dung CHUNG cho ca hai duong kich hoat — thu tien tay (MembershipService) va thanh toan
// online (VnPayService). Truoc day moi service giu mot ban sao giong het nhau; sua mot ben
// quen ben kia se lam hai luong kich hoat goi khac nhau.
internal static class MembershipActivation
{
    // Huy cac don PendingPayment "anh em" cua cung member (giu lai don dang duoc kich hoat).
    public static async Task CancelSiblingPendingAsync(
        GymMasterDbContext dbContext,
        long memberId,
        long keepId,
        CancellationToken cancellationToken)
    {
        var siblings = await dbContext.Memberships
            .Where(item => item.MemberId == memberId &&
                item.Id != keepId &&
                item.Status == MembershipStatus.PendingPayment)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblings)
        {
            sibling.Status = MembershipStatus.Cancelled;
            sibling.UpdatedAt = DateTime.UtcNow;
        }
    }

    // Ghi thay doi khi kich hoat. Neu co goi Active cu bi thay the (noi han) thi phai ghi
    // LAM HAI LUOT trong mot transaction: ha don moi ve PendingPayment -> ghi goi cu thanh
    // Cancelled -> nang don moi len Active. Ghi mot luot se co khoanh khac HAI membership
    // cung Active cho mot member, vi pham rang buoc o DB.
    //
    // Nhanh !IsRelational() danh cho test EF InMemory (khong ho tro transaction).
    public static async Task SaveActivationAsync(
        GymMasterDbContext dbContext,
        Membership membership,
        Membership? replacedActive,
        CancellationToken cancellationToken)
    {
        if (replacedActive is null || !dbContext.Database.IsRelational())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var finalStatus = membership.Status;
        membership.Status = MembershipStatus.PendingPayment;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        membership.Status = finalStatus;
        membership.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
