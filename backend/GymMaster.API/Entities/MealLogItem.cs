namespace GymMaster.API.Entities;

/// <summary>
/// Ánh xạ bảng meal_log_items: một món cụ thể nằm trong một MealLog.
/// Lưu snapshot Quantity và Calories tại thời điểm ghi bữa.
/// </summary>
public sealed class MealLogItem
{
    /// <summary>Khóa chính của dòng món.</summary>
    public long Id { get; set; }

    /// <summary>Khóa ngoại tới bữa ăn cha.</summary>
    public long MealLogId { get; set; }

    /// <summary>Khóa ngoại tới món trong food_items.</summary>
    public long FoodItemId { get; set; }

    /// <summary>Số khẩu phần/đơn vị mà hội viên đã ăn.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Calo đã tính: FoodItem.CaloriesPerUnit × Quantity.</summary>
    public decimal Calories { get; set; }

    /// <summary>Navigation property để lấy tên và macro của món khi trả response.</summary>
    public FoodItem FoodItem { get; set; } = null!;
}
