namespace GymMaster.API.Options;

public sealed class CheckInOptions
{
    public const string SectionName = "CheckIn";

    // FR-CHK-02/03: bat kiem tra membership Active con han truoc khi cho check-in.
    // Mac dinh false vi module Membership (spec 003) co the chua trien khai tren DB hien tai.
    // Dat true khi bang `memberships` da co du lieu de gac theo dung spec.
    public bool EnforceMembership { get; set; }

    // FR-CHK-03 (OQ-06): gioi han 1 check-in/ngay. Mac dinh false (MVP cho nhieu lan/ngay).
    // Khi true se uu tien hon MaxPerDay (tuong duong MaxPerDay = 1) — giu tuong thich cau hinh cu.
    public bool OncePerDay { get; set; }

    // FR-CHK-03: so lan check-in toi da trong 1 ngay (theo gio VN). Mac dinh 2.
    // <= 0 nghia la khong gioi han. Bi OncePerDay ghi de neu OncePerDay = true.
    public int MaxPerDay { get; set; } = 2;

    // Gioi han check-in/ngay thuc te sau khi gop OncePerDay (cu) va MaxPerDay.
    // Tra ve 0 khi khong gioi han.
    public int EffectiveMaxPerDay => OncePerDay ? 1 : Math.Max(0, MaxPerDay);
}
