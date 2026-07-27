namespace GymMaster.API.Entities;

/// <summary>
/// Ánh xạ bảng food_items: danh mục món dùng cho tìm kiếm, quét AI và ghi bữa.
/// FoodItemService quản lý món thủ công; FoodScanService có thể tạo món Source="AI".
/// </summary>
public sealed class FoodItem
{
    /// <summary>Khóa chính.</summary>
    public long Id { get; set; }

    /// <summary>Tên món duy nhất trong kho.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Đơn vị của khẩu phần, ví dụ g hoặc phần.</summary>
    public string Unit { get; set; } = null!;

    /// <summary>Số kcal trên một đơn vị/khẩu phần chuẩn.</summary>
    public decimal CaloriesPerUnit { get; set; }

    /// <summary>Protein trên một đơn vị chuẩn, đơn vị gram.</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Carbohydrate trên một đơn vị chuẩn, đơn vị gram.</summary>
    public decimal? CarbG { get; set; }

    /// <summary>Chất béo trên một đơn vị chuẩn, đơn vị gram.</summary>
    public decimal? FatG { get; set; }

    /// <summary>Chỉ món active mới xuất hiện trong tìm kiếm và được ghi vào bữa.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm tạo món theo UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Khối lượng khẩu phần chuẩn; món AI dùng mặc định 100 gram.</summary>
    public decimal ServingSize { get; set; } = 100;

    /// <summary>Nguồn tạo món, hiện dùng "Admin" hoặc "AI".</summary>
    public string Source { get; set; } = "Admin";
}
