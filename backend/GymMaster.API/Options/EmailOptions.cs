namespace GymMaster.API.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    // SMTP server. Gmail: smtp.gmail.com / 587 (STARTTLS).
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    // Tai khoan gui (Gmail cua ban). Mat khau dung App Password 16 ky tu (KHONG phai mat khau Gmail thuong).
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "GymMaster";

    public string AppPassword { get; set; } = string.Empty;

    // Goc URL frontend de dung link reset: http://localhost:3000
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";

    // Bat/tat gui mail. Neu chua cau hinh (AppPassword rong) thi coi nhu chua bat.
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SenderEmail) && !string.IsNullOrWhiteSpace(AppPassword);
}
