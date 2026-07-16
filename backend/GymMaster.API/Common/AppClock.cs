namespace GymMaster.API.Common;
// Nguon su that DUY NHAT cho "hom nay" cua he thong.
// Gym 1 chi nhanh tai Viet Nam -> tinh theo gio VN (GMT+7), khong phai UTC,
// tranh lech 1 ngay (vd het han membership, ngay calo) vao buoi sang gio VN.
public static class AppClock
{
    public static DateOnly Today() => DateOnly.FromDateTime(NowVn());

    public static DateTime NowVn() => DateTime.UtcNow.AddHours(7);
}
