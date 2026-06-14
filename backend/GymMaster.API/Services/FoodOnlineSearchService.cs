using System.Globalization;
using System.Text.Json;
using GymMaster.API.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace GymMaster.API.Services;

// Goi Open Food Facts de tim mon, co nho tam (cache) + loi-thi-rong.
public sealed class FoodOnlineSearchService : IFoodOnlineSearchService
{
    private const string SourceName = "open-food-facts";
    private const int NameMaxLength = 150;
    private const int UnitMaxLength = 30;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public FoodOnlineSearchService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<IReadOnlyList<OnlineFoodItemResponse>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        var keyword = (query ?? string.Empty).Trim();
        if (keyword.Length < 2)
        {
            return Array.Empty<OnlineFoodItemResponse>();
        }

        // Nho tam: tu khoa nay vua tim trong vai gio qua thi tra luon, khong goi lai web.
        var cacheKey = $"online-food:{keyword.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<OnlineFoodItemResponse>? cached) && cached is not null)
        {
            return cached;
        }

        IReadOnlyList<OnlineFoodItemResponse> result;
        try
        {
            var url = "cgi/search.pl?search_simple=1&action=process&json=1&page_size=10"
                + $"&search_terms={Uri.EscapeDataString(keyword)}";
            var json = await _http.GetStringAsync(url, cancellationToken);
            result = ParseProducts(json);
        }
        catch
        {
            // Loi mang / timeout / bi chan -> tra rong, FE tu lui ve kho noi bo.
            return Array.Empty<OnlineFoodItemResponse>();
        }

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    // Boc tach JSON cua Open Food Facts -> danh sach mon. Tach static de test duoc khong can mang.
    public static IReadOnlyList<OnlineFoodItemResponse> ParseProducts(string json)
    {
        var items = new List<OnlineFoodItemResponse>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("products", out var products) ||
            products.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (var product in products.EnumerateArray())
        {
            var name = GetString(product, "product_name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue; // bo qua mon khong co ten
            }

            decimal? calories = null, protein = null, carb = null, fat = null;
            if (product.TryGetProperty("nutriments", out var nutriments))
            {
                calories = GetDecimal(nutriments, "energy-kcal_100g");
                protein = GetDecimal(nutriments, "proteins_100g");
                carb = GetDecimal(nutriments, "carbohydrates_100g");
                fat = GetDecimal(nutriments, "fat_100g");
            }

            items.Add(new OnlineFoodItemResponse(
                Truncate(name, NameMaxLength),
                Truncate(GetString(product, "serving_size") ?? "100g", UnitMaxLength),
                NonNegative(calories) ?? 0,
                NonNegative(protein),
                NonNegative(carb),
                NonNegative(fat),
                SourceName));
        }

        return items;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    // OFF tra so co the la number HOAC string -> xu ly ca hai.
    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? NonNegative(decimal? value)
    {
        return value is null ? null : Math.Max(0, value.Value);
    }

    private static string Truncate(string value, int max)
    {
        value = value.Trim();
        return value.Length <= max ? value : value[..(max - 3)] + "...";
    }
}
