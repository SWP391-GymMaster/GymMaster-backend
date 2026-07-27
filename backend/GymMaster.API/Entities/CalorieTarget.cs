namespace GymMaster.API.Entities;

/// <summary>
/// Ánh xạ bảng calorie_targets: mục tiêu calo và macro của hội viên theo ngày hiệu lực.
/// Một hội viên chỉ có một bản ghi cho mỗi EffectiveDate.
/// </summary>
public sealed class CalorieTarget
{
    /// <summary>Khóa chính.</summary>
    public long Id { get; set; }

    /// <summary>Khóa ngoại tới MemberProfile sở hữu mục tiêu.</summary>
    public long MemberId { get; set; }

    /// <summary>Ngày bắt đầu áp dụng mục tiêu này.</summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Tổng kcal mục tiêu mỗi ngày; bắt buộc lớn hơn 0.</summary>
    public decimal DailyCalories { get; set; }

    /// <summary>Protein mục tiêu mỗi ngày, đơn vị gram; null nếu không đặt.</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Carbohydrate mục tiêu mỗi ngày, đơn vị gram; null nếu không đặt.</summary>
    public decimal? CarbG { get; set; }

    /// <summary>Chất béo mục tiêu mỗi ngày, đơn vị gram; null nếu không đặt.</summary>
    public decimal? FatG { get; set; }

    /// <summary>Thời điểm tạo bản ghi theo UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
