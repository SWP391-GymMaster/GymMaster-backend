using GymMaster.API.Services;
using Xunit;

namespace GymMaster.Api.Tests;

// Test boc tach JSON cua Open Food Facts (phan tinh, khong can mang).
public class FoodOnlineSearchServiceTests
{
    [Fact] // Map mon co ten; bo qua mon khong ten; calo/macro dung
    public void ParseProducts_maps_named_products_only()
    {
        var json = """
        {
          "products": [
            {
              "product_name": "TH True Milk",
              "serving_size": "180ml",
              "nutriments": { "energy-kcal_100g": 60, "proteins_100g": 3.2, "carbohydrates_100g": 4.8, "fat_100g": 3.5 }
            },
            { "nutriments": { "energy-kcal_100g": 100 } }
          ]
        }
        """;

        var items = FoodOnlineSearchService.ParseProducts(json);

        var item = Assert.Single(items); // mon khong ten bi bo qua
        Assert.Equal("TH True Milk", item.Name);
        Assert.Equal("180ml", item.Unit);
        Assert.Equal(60m, item.CaloriesPerUnit);
        Assert.Equal(3.2m, item.ProteinG);
        Assert.Equal(4.8m, item.CarbG);
        Assert.Equal(3.5m, item.FatG);
        Assert.Equal("open-food-facts", item.Source);
    }

    [Fact] // Lam sach: so am -> 0; so dang chuoi van doc duoc
    public void ParseProducts_cleans_negative_and_string_numbers()
    {
        var json = """
        {
          "products": [
            { "product_name": "Mon test", "nutriments": { "energy-kcal_100g": "250", "proteins_100g": -5 } }
          ]
        }
        """;

        var item = Assert.Single(FoodOnlineSearchService.ParseProducts(json));
        Assert.Equal(250m, item.CaloriesPerUnit); // chuoi "250" -> 250
        Assert.Equal(0m, item.ProteinG);          // -5 -> 0
    }
}
