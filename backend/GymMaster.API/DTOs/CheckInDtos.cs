namespace GymMaster.API.DTOs;

// Spec 004 — Check-in (FR-CHK-01..06)

public sealed class CreateCheckInRequest
{
    // Uu tien MemberId (= member_profiles.Id). MemberCode tra cuu theo SDT (schema khong co ma rieng).
    public long? MemberId { get; set; }

    public string? MemberCode { get; set; }

    // FE gui "front-desk"/"member" lam goi y; nguon that suy ra tu role (chong gia mao).
    public string? Source { get; set; }
}

// Khop MockCheckInDto cua frontend: { id, memberId, checkInAt, source }.
public sealed record CheckInResponse(
    long Id,
    long MemberId,
    DateTime CheckInAt,
    string Source);
