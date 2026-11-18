using System.Globalization;
using System.Text.Json;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Services.Analytics;

/// <summary>Calculations about the menu itself: which products carry the menu,
/// which ones never move, when to reorder them and which extras customers
/// ask for the most.</summary>
public static class AnalyticsMenuCalculators
{
    private static readonly JsonSerializerOptions _jsonOptions = new();

    /// <summary>Classifies every sold product as نجمة, حصان, لغز or راكد. A
    /// product is popular when it sells at least the median quantity of the
    /// products that moved, and profitable when it earns at least the average
    /// revenue per unit.</summary>
    public static IReadOnlyList<AnalyticsRow> MenuEngineering(AnalyticsInput input)
    {
        var sold = SoldBuckets(input.ReceivedOrders);
        if (sold.Count == 0)
            return Array.Empty<AnalyticsRow>();

        var medianUnits = Median(sold.Values.Select(b => (double)b.Units).ToList());
        var meanPerUnit = sold.Values.Average(b => b.Units == 0 ? 0m : b.Revenue / b.Units);

        var rows = new List<AnalyticsRow>(sold.Count);
        var rank = 1;
        foreach (var b in sold.Values.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.Units))
        {
            var perUnit = b.Units == 0 ? 0m : b.Revenue / b.Units;
            var popular = b.Units >= medianUnits;
            var profitable = perUnit >= meanPerUnit;
            var classification = popular
                ? (profitable ? "نجمة" : "حصان")
                : (profitable ? "لغز" : "راكد");

            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = b.Name,
                Secondary = b.Category,
                Tertiary = $"التصنيف: {classification} | إيراد الوحدة: {AnalyticsFormat.Money(perUnit, input.Currency)}",
                Value = $"الكمية: {AnalyticsFormat.Number(b.Units)} | {AnalyticsFormat.Money(b.Revenue, input.Currency)}",
                BarRatio = AnalyticsSalesCalculators.Ratio(b.Revenue, AnalyticsSalesCalculators.Max(sold.Values.Select(x => x.Revenue)))
            });
            rank++;
        }

        return rows;
    }

    /// <summary>The products that still sit in the stock but were not sold in
    /// the reported range. The date of the last sale comes from the whole
    /// history so the report shows how long a product has been standing still.</summary>
    public static IReadOnlyList<AnalyticsRow> DeadStock(AnalyticsInput input)
    {
        var soldKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in input.ReceivedOrders)
        {
            foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
                soldKeys.Add(AnalyticsSalesCalculators.ProductKey(item));
        }

        var lastSold = LastSoldDates(input.AllOrders);

        var rows = new List<AnalyticsRow>();
        var rank = 1;
        foreach (var product in input.Products.Where(p => p.Inventory > 0).OrderByDescending(p => p.Inventory))
        {
            if (soldKeys.Contains(product.Id) || soldKeys.Contains(product.NameAr))
                continue;

            var key = !string.IsNullOrWhiteSpace(product.Id) ? product.Id : product.NameAr;
            DateTime? soldAt = null;
            if (lastSold.TryGetValue(key, out var found))
                soldAt = found;

            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = product.NameAr,
                Secondary = string.IsNullOrWhiteSpace(product.Category) ? "أخرى" : product.Category,
                Tertiary = soldAt.HasValue
                    ? $"آخر بيع: {soldAt.Value:yyyy/MM/dd}"
                    : "آخر بيع: لم يُبَع قط",
                Value = $"المخزون: {AnalyticsFormat.Number(product.Inventory)}",
                BarRatio = AnalyticsSalesCalculators.Ratio(product.Inventory, AnalyticsSalesCalculators.Max(input.Products.Select(p => (decimal)p.Inventory)))
            });
            rank++;
        }

        return rows;
    }

    /// <summary>The products whose stock runs out within three days at the
    /// current sales speed, with the quantity that should be ordered. The
    /// suggestion is only shown on screen and is never written to the database.</summary>
    public static IReadOnlyList<AnalyticsRow> ReorderPriority(AnalyticsInput input)
    {
        var sold = SoldBuckets(input.ReceivedOrders);
        var days = Math.Max(1, input.RangeDays);

        var rows = new List<AnalyticsRow>();
        foreach (var product in input.Products)
        {
            var key = !string.IsNullOrWhiteSpace(product.Id) ? product.Id : product.NameAr;
            if (!sold.TryGetValue(key, out var bucket) || bucket.Units <= 0)
                continue;

            var daily = bucket.Units / (double)days;
            if (daily <= 0)
                continue;

            var coverage = product.Inventory / daily;
            if (coverage >= 3d)
                continue;

            var suggested = Math.Max(0, product.MaxInventory - product.Inventory);
            rows.Add(new AnalyticsRow
            {
                Primary = product.NameAr,
                Secondary = string.IsNullOrWhiteSpace(product.Category) ? "أخرى" : product.Category,
                Tertiary = $"متوسط يومي: {daily:F2} | تغطية: {coverage:F1} يوم",
                Value = $"المخزون: {AnalyticsFormat.Number(product.Inventory)} | مقترح: {AnalyticsFormat.Number(suggested)}",
                BarRatio = Math.Min(1d, 1d / Math.Max(0.1d, coverage))
            });
        }

        return rows
            .OrderBy(r => r.Tertiary, StringComparer.Ordinal)
            .Select((r, i) => new AnalyticsRow
            {
                Rank = (i + 1).ToString(CultureInfo.InvariantCulture),
                Primary = r.Primary,
                Secondary = r.Secondary,
                Tertiary = r.Tertiary,
                Value = r.Value,
                BarRatio = r.BarRatio
            })
            .ToList();
    }

    /// <summary>The extra options customers ask for the most, with the extra
    /// revenue they bring in.</summary>
    public static IReadOnlyList<AnalyticsRow> FavouriteCustomizations(AnalyticsInput input)
    {
        var map = new Dictionary<string, (int Count, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);

        foreach (var order in input.ReceivedOrders)
        {
            foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
            {
                foreach (var option in ParseCustomizations(item.SelectedCustomizationsJson))
                {
                    var name = !string.IsNullOrWhiteSpace(option.NameAr)
                        ? option.NameAr
                        : (!string.IsNullOrWhiteSpace(option.NameEn) ? option.NameEn : option.Id);

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    map.TryGetValue(name, out var current);
                    current.Count += 1;
                    current.Revenue += option.Price;
                    map[name] = current;
                }
            }
        }

        var max = Math.Max(1, map.Values.Count == 0 ? 1 : map.Values.Max(v => v.Count));
        var rows = new List<AnalyticsRow>();
        var rank = 1;
        foreach (var entry in map.OrderByDescending(kvp => kvp.Value.Count).ThenBy(kvp => kvp.Key))
        {
            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = entry.Key,
                Secondary = $"إيراد إضافي: {AnalyticsFormat.Money(entry.Value.Revenue, input.Currency)}",
                Tertiary = $"الاستخدام: {AnalyticsFormat.Number(entry.Value.Count)}",
                Value = $"الحصة: {AnalyticsFormat.Percent((double)entry.Value.Count / (double)max)}",
                BarRatio = (double)entry.Value.Count / (double)max
            });
            rank++;
        }

        return rows;
    }

    private static Dictionary<string, AnalyticsSalesCalculators.ItemBucket> SoldBuckets(IEnumerable<Order> orders)
    {
        var map = new Dictionary<string, AnalyticsSalesCalculators.ItemBucket>(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
            {
                var key = AnalyticsSalesCalculators.ProductKey(item);
                if (!map.TryGetValue(key, out var current))
                {
                    map[key] = new AnalyticsSalesCalculators.ItemBucket(
                        item.ProductNameAr,
                        string.IsNullOrWhiteSpace(item.ProductCategory) ? "أخرى" : item.ProductCategory,
                        item.Quantity,
                        0,
                        AnalyticsSalesCalculators.LineRevenue(item));
                    continue;
                }

                map[key] = current with
                {
                    Units = current.Units + item.Quantity,
                    Revenue = current.Revenue + AnalyticsSalesCalculators.LineRevenue(item)
                };
            }
        }

        return map;
    }

    private static Dictionary<string, DateTime> LastSoldDates(IEnumerable<Order> orders)
    {
        var map = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var order in orders)
        {
            if (order.Status != OrderStatus.Received)
                continue;

            foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
            {
                var key = AnalyticsSalesCalculators.ProductKey(item);
                if (!map.TryGetValue(key, out var last) || order.Timestamp > last)
                    map[key] = order.Timestamp;
            }
        }

        return map;
    }

    private static IEnumerable<CustomizationOption> ParseCustomizations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return Array.Empty<CustomizationOption>();

        try
        {
            return JsonSerializer.Deserialize<List<CustomizationOption>>(json, _jsonOptions) ?? new List<CustomizationOption>();
        }
        catch (JsonException)
        {
            return Array.Empty<CustomizationOption>();
        }
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
            return 0d;

        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2d;
    }
}
