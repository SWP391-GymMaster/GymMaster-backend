namespace GymMaster.API.Entities;

/// <summary>
/// Ánh xạ bảng meal_logs: phần đầu của một bữa ăn theo hội viên, ngày và loại bữa.
/// Các món cụ thể nằm trong collection Items (bảng meal_log_items).
/// </summary>
public sealed class MealLog
{
    /// <summary>Khóa chính của bữa.</summary>
    public long Id { get; set; }

    /// <summary>Hội viên sở hữu nhật ký.</summary>
    public long MemberId { get; set; }

    /// <summary>Ngày ăn theo lịch, không chứa giờ.</summary>
    public DateOnly LogDate { get; set; }

    /// <summary>Loại bữa: sáng, trưa, tối hoặc ăn nhẹ.</summary>
    public MealType MealType { get; set; }

    /// <summary>Thời điểm tạo bản ghi theo UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Các món thuộc bữa; EF Core nạp/lưu qua quan hệ một-nhiều.</summary>
    public List<MealLogItem> Items { get; set; } = [];
}
