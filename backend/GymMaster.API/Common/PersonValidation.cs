namespace GymMaster.API.Common;
// Luat chung cho thong tin ca nhan cua MOI actor (member/PT/staff/admin).
// Mot nguon su that duy nhat phia BE: cac service (Member/Trainer/User/Account)
// cung goi truoc khi ghi DB. Gioi han do dai khop dung cot DB de khong bao gio
// no SqlException truncation (500) — thay vao do tra VALIDATION_ERROR 400 ro rang.
public static class PersonValidation
{
    public const int FullNameMaxLength = 150;
    public const int PhoneMaxLength = 30;
    public const int GenderMaxLength = 20;
    public const int AddressMaxLength = 255;
    public const int EmergencyContactMaxLength = 100;
    public const int SpecialtyMaxLength = 150;
    public const int BioMaxLength = 1000;

    private static readonly DateTime MinDateOfBirth = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Tra ve thong bao loi neu vi pham, null neu hop le. Tham so nao null thi bo qua
    // (khop ngu nghia partial-update cua cac request DTO).
    public static string? Validate(
        string? fullName = null,
        string? phone = null,
        DateTime? dateOfBirth = null,
        string? gender = null,
        string? address = null,
        string? emergencyContact = null,
        string? specialty = null,
        string? bio = null)
    {
        if (fullName is not null && fullName.Trim().Length > FullNameMaxLength)
        {
            return $"Ho ten toi da {FullNameMaxLength} ky tu.";
        }

        if (phone is not null && phone.Trim().Length > PhoneMaxLength)
        {
            return $"So dien thoai toi da {PhoneMaxLength} ky tu.";
        }

        if (dateOfBirth is not null && dateOfBirth.Value > DateTime.UtcNow)
        {
            return "Ngay sinh khong duoc o tuong lai.";
        }

        if (dateOfBirth is not null && dateOfBirth.Value < MinDateOfBirth)
        {
            return "Ngay sinh khong hop le (truoc nam 1900).";
        }

        if (gender is not null && gender.Trim().Length > GenderMaxLength)
        {
            return $"Gioi tinh toi da {GenderMaxLength} ky tu.";
        }

        if (address is not null && address.Trim().Length > AddressMaxLength)
        {
            return $"Dia chi toi da {AddressMaxLength} ky tu.";
        }

        if (emergencyContact is not null && emergencyContact.Trim().Length > EmergencyContactMaxLength)
        {
            return $"Lien he khan cap toi da {EmergencyContactMaxLength} ky tu.";
        }

        if (specialty is not null && specialty.Trim().Length > SpecialtyMaxLength)
        {
            return $"Chuyen mon toi da {SpecialtyMaxLength} ky tu.";
        }

        if (bio is not null && bio.Trim().Length > BioMaxLength)
        {
            return $"Gioi thieu toi da {BioMaxLength} ky tu.";
        }

        return null;
    }
}
