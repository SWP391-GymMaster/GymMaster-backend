namespace GymMaster.API.Entities;

public sealed class MealLogItem
{
    public long Id { get; set; }

    public long MealLogId { get; set; }

    public long FoodItemId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Calories { get; set; }

    public FoodItem FoodItem { get; set; } = null!;
}
