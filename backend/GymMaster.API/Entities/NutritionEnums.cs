namespace GymMaster.API.Entities;

/// <summary>
/// Loại bữa được lưu dạng tinyint trong meal_logs và nhận dạng byte từ API 05.
/// Giá trị số là contract với database/frontend nên không được đổi tùy ý.
/// </summary>
public enum MealType : byte
{
    /// <summary>Bữa sáng.</summary>
    Breakfast = 1,

    /// <summary>Bữa trưa.</summary>
    Lunch = 2,

    /// <summary>Bữa tối.</summary>
    Dinner = 3,

    /// <summary>Bữa phụ/ăn nhẹ.</summary>
    Snack = 4
}
