namespace GymMaster.API.Entities;

/// <summary>
/// Ánh xạ bảng audit_logs: lưu dấu vết các hành động thay đổi dữ liệu quan trọng.
/// AuditService tạo entity này; DashboardService đọc để trả API 13.
/// </summary>
public sealed class AuditLog
{
    /// <summary>Khóa chính của bản ghi log.</summary>
    public long Id { get; set; }

    /// <summary>User thực hiện; có thể null nếu request không có claim hợp lệ.</summary>
    public long? UserId { get; set; }

    /// <summary>Mã hành động, ví dụ CREATE_FOOD hoặc SET_CALORIE_TARGET.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Loại đối tượng bị tác động, ví dụ FoodItem hoặc MealLog.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>Id của đối tượng bị tác động.</summary>
    public long EntityId { get; set; }

    /// <summary>Thông tin truy vết bổ sung dạng JSON; không chứa PII nhạy cảm.</summary>
    public string? Metadata { get; set; }

    /// <summary>Thời điểm ghi log theo UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
