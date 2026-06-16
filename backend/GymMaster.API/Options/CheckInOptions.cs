namespace GymMaster.API.Options;

public sealed class CheckInOptions
{
    public const string SectionName = "CheckIn";

    // FR-CHK-02/03: bat kiem tra membership Active con han truoc khi cho check-in.
    // Mac dinh false vi module Membership (spec 003) co the chua trien khai tren DB hien tai.
    // Dat true khi bang `memberships` da co du lieu de gac theo dung spec.
    public bool EnforceMembership { get; set; }

    // FR-CHK-03 (OQ-06): gioi han 1 check-in/ngay. Mac dinh false (MVP cho nhieu lan/ngay).
    public bool OncePerDay { get; set; }
}
