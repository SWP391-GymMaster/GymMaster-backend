namespace GymMaster.API.Features.CheckIns;
// Spec 004 — Check-in (FR-CHK-01..06)

public sealed class CreateCheckInRequest
{
    // Uu tien MemberId (= member_profiles.Id). MemberCode tra cuu theo SDT (schema khong co ma rieng).
    public long? MemberId { get; set; }

    public string? MemberCode { get; set; }

    // FE gui "front-desk"/"member" lam goi y; nguon that suy ra tu role (chong gia mao).
    public string? Source { get; set; }
}

// Khop MockCheckInDto cua frontend: { id, memberId, checkInAt, source, memberName? }.
// MemberName chi duoc dien o cac endpoint LIST (de hien ten o "check-in gan day");
// POST tra ve null (FE khong dung ten o ket qua check-in).
public sealed record CheckInResponse(
    long Id,
    long MemberId,
    DateTime CheckInAt,
    string Source,
    string? MemberName = null);
