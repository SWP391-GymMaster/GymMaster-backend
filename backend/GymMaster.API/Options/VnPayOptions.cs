namespace GymMaster.API.Options;

// Cau hinh cong thanh toan VNPay (sandbox). Secret (TmnCode/HashSecret) dat trong User Secrets,
// phan URL/version de o appsettings. Doi sang live chi can doi BaseUrl + TmnCode + HashSecret.
public sealed class VnPayOptions
{
    public const string SectionName = "VnPay";

    // Ma website (merchant) do VNPay cap.
    public string TmnCode { get; set; } = string.Empty;

    // Chuoi bi mat de ky HMAC-SHA512.
    public string HashSecret { get; set; } = string.Empty;

    // Endpoint trang thanh toan. Sandbox: https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    // Noi VNPay redirect trinh duyet ve sau khi thanh toan (trang ket qua FE hoac endpoint return cua BE).
    public string ReturnUrl { get; set; } = string.Empty;

    public string Version { get; set; } = "2.1.0";

    public string Command { get; set; } = "pay";

    public string CurrCode { get; set; } = "VND";

    public string Locale { get; set; } = "vn";
}
