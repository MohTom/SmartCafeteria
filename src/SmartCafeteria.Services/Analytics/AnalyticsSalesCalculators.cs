using System.Globalization;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Services.Analytics;

/// <summary>Shared formatting for the analytical reports. The numbers are
/// formatted with the invariant culture so the reports read the same on every
/// machine, while the Arabic labels stay untouched.</summary>
public static class AnalyticsFormat
{
    /// <summary>Formats an amount with its currency, for example "1,250.50 SDG".</summary>
    public static string Money(decimal value, string currency)
        => $"{value.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    /// <summary>Formats a whole number, for example "1,250".</summary>
    public static string Number(int value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Formats a ratio between 0 and 1 as a percentage.</summary>
    public static string Percent(double ratio)
        => ratio.ToString("P1", CultureInfo.InvariantCulture);
}

/// <summary>Calculations for the sales side of the analytics: what sells, where,
/// how it was paid for, who served it and when it sells best.</summary>
public static class AnalyticsSalesCalculators
{
    /// <summary>Revenue of one sold line: the price of the product times the
    /// quantity plus the extra paid for its customizations.</summary>
    public static decimal LineRevenue(OrderItem item)
        => item.ProductPrice * item.Quantity + item.CustomizationsTotal;

    /// <summary>The best selling products, ranked by revenue.</summary>
    public static IReadOnlyList<AnalyticsRow> TopItems(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .SelectMany(o => o.Items ?? Enumerable.Empty<OrderItem>())
            .GroupBy(i => ProductKey(i), StringComparer.Ordinal)
            .Select(g => new ItemBucket(
                g.Select(i => i.ProductNameAr).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                g.Select(i => i.ProductCategory).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? "أخرى",
                g.Sum(i => i.Quantity),
                0,
                g.Sum(LineRevenue)))
            .OrderByDescending(b => b.Revenue)
            .ThenByDescending(b => b.Units)
            .ToList();

        return Rank(buckets, input.Currency);
    }

    /// <summary>The revenue of every category of the menu.</summary>
    public static IReadOnlyList<AnalyticsRow> SalesByCategory(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .SelectMany(o => (o.Items ?? Enumerable.Empty<OrderItem>()).Select(i => (Order: o, Item: i)))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Item.ProductCategory) ? "أخرى" : x.Item.ProductCategory, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ItemBucket(
                g.Key,
                string.Empty,
                g.Sum(x => x.Item.Quantity),
                g.Select(x => x.Order.Id).Distinct().Count(),
                g.Sum(x => LineRevenue(x.Item))))
            .OrderByDescending(b => b.Revenue)
            .ToList();

        var max = Max(buckets.Select(b => b.Revenue));
        var rows = new List<AnalyticsRow>(buckets.Count);
        var rank = 1;
        foreach (var b in buckets)
        {
            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = b.Name,
                Secondary = $"الطلبات: {AnalyticsFormat.Number(b.Orders)}",
                Tertiary = $"الكمية: {AnalyticsFormat.Number(b.Units)}",
                Value = AnalyticsFormat.Money(b.Revenue, input.Currency),
                BarRatio = Ratio(b.Revenue, max)
            });
            rank++;
        }

        return rows;
    }

    /// <summary>How the orders were paid for.</summary>
    public static IReadOnlyList<AnalyticsRow> PaymentMethods(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new ItemBucket(PaymentLabel(g.Key), string.Empty, 0, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(b => b.Revenue)
            .ToList();

        return ShareRows(buckets, input);
    }

    /// <summary>How many orders were eaten in (محلي) versus taken away (سفري).</summary>
    public static IReadOnlyList<AnalyticsRow> OrderTypes(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => o.OrderType)
            .Select(g => new ItemBucket(g.Key.ToArabic(), string.Empty, 0, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(b => b.Revenue)
            .ToList();

        return ShareRows(buckets, input);
    }

    /// <summary>Orders that carry a note versus orders that do not.</summary>
    public static IReadOnlyList<AnalyticsRow> OrdersWithNotes(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => string.IsNullOrWhiteSpace(o.Notes) ? "بدون ملاحظات" : "بها ملاحظات")
            .Select(g => new ItemBucket(g.Key, string.Empty, 0, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(b => b.Revenue)
            .ToList();

        return ShareRows(buckets, input);
    }

    /// <summary>What every cashier achieved.</summary>
    public static IReadOnlyList<AnalyticsRow> CashierPerformance(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => string.IsNullOrWhiteSpace(o.CashierName) ? "غير محدد" : o.CashierName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ItemBucket(g.Key, string.Empty, 0, g.Count(), g.Sum(o => o.Total)))
            .OrderByDescending(b => b.Revenue)
            .ToList();

        var max = Max(buckets.Select(b => b.Revenue));
        var rows = new List<AnalyticsRow>(buckets.Count);
        var rank = 1;
        foreach (var b in buckets)
        {
            var average = b.Orders == 0 ? 0m : b.Revenue / b.Orders;
            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = b.Name,
                Secondary = $"متوسط الطلب: {AnalyticsFormat.Money(average, input.Currency)}",
                Tertiary = $"الطلبات: {AnalyticsFormat.Number(b.Orders)}",
                Value = AnalyticsFormat.Money(b.Revenue, input.Currency),
                BarRatio = Ratio(b.Revenue, max)
            });
            rank++;
        }

        return rows;
    }

    /// <summary>The sales of every hour of the day that had orders.</summary>
    public static IReadOnlyList<AnalyticsRow> SalesByHour(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => o.Timestamp.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new ItemBucket(
                $"{g.Key:00}:00 - {(g.Key + 1) % 24:00}:00",
                string.Empty,
                g.Sum(o => (o.Items ?? Enumerable.Empty<OrderItem>()).Sum(i => i.Quantity)),
                g.Count(),
                g.Sum(o => o.Total)))
            .ToList();

        return Simple(buckets, input.Currency);
    }

    /// <summary>The sales of every day of the range.</summary>
    public static IReadOnlyList<AnalyticsRow> SalesByDay(AnalyticsInput input)
    {
        var buckets = input.ReceivedOrders
            .GroupBy(o => o.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ItemBucket(
                g.Key.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                string.Empty,
                g.Sum(o => (o.Items ?? Enumerable.Empty<OrderItem>()).Sum(i => i.Quantity)),
                g.Count(),
                g.Sum(o => o.Total)))
            .ToList();

        return Simple(buckets, input.Currency);
    }

    /// <summary>The sales of every day of the week.</summary>
    public static IReadOnlyList<AnalyticsRow> SalesByWeekday(AnalyticsInput input)
    {
        var days = new[]
        {
            DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
        };

        var buckets = days
            .Select(day =>
            {
                var orders = input.ReceivedOrders.Where(o => o.Timestamp.DayOfWeek == day).ToList();
                return new ItemBucket(
                    WeekdayLabel(day),
                    string.Empty,
                    orders.Sum(o => (o.Items ?? Enumerable.Empty<OrderItem>()).Sum(i => i.Quantity)),
                    orders.Count,
                    orders.Sum(o => o.Total));
            })
            .ToList();

        return Simple(buckets, input.Currency);
    }

    /// <summary>Rows of a report whose rows are shares of the whole: the percentage,
    /// the number of orders and the money, in the same shape everywhere.</summary>
    private static IReadOnlyList<AnalyticsRow> ShareRows(IReadOnlyList<ItemBucket> buckets, AnalyticsInput input)
    {
        var total = input.ReceivedOrders.Sum(o => o.Total);
        var max = Max(buckets.Select(b => b.Revenue));
        var rows = new List<AnalyticsRow>(buckets.Count);
        foreach (var b in buckets)
        {
            var share = total <= 0m ? 0d : (double)(b.Revenue / total);
            rows.Add(new AnalyticsRow
            {
                Primary = b.Name,
                Secondary = $"النسبة: {AnalyticsFormat.Percent(share)}",
                Tertiary = $"الطلبات: {AnalyticsFormat.Number(b.Orders)}",
                Value = AnalyticsFormat.Money(b.Revenue, input.Currency),
                BarRatio = Ratio(b.Revenue, max)
            });
        }

        return rows;
    }

    private static IReadOnlyList<AnalyticsRow> Rank(List<ItemBucket> buckets, string currency)
    {
        var max = Max(buckets.Select(b => b.Revenue));
        var rows = new List<AnalyticsRow>(buckets.Count);
        var rank = 1;
        foreach (var b in buckets)
        {
            rows.Add(new AnalyticsRow
            {
                Rank = rank.ToString(CultureInfo.InvariantCulture),
                Primary = b.Name,
                Secondary = b.Category,
                Tertiary = $"الكمية: {AnalyticsFormat.Number(b.Units)}",
                Value = AnalyticsFormat.Money(b.Revenue, currency),
                BarRatio = Ratio(b.Revenue, max)
            });
            rank++;
        }

        return rows;
    }

    private static IReadOnlyList<AnalyticsRow> Simple(List<ItemBucket> buckets, string currency)
    {
        var max = Max(buckets.Select(b => b.Revenue));
        var rows = new List<AnalyticsRow>(buckets.Count);
        foreach (var b in buckets)
        {
            rows.Add(new AnalyticsRow
            {
                Primary = b.Name,
                Secondary = $"القطع: {AnalyticsFormat.Number(b.Units)}",
                Tertiary = $"الطلبات: {AnalyticsFormat.Number(b.Orders)}",
                Value = AnalyticsFormat.Money(b.Revenue, currency),
                BarRatio = Ratio(b.Revenue, max)
            });
        }

        return rows;
    }

    internal static string ProductKey(OrderItem item)
        => !string.IsNullOrWhiteSpace(item.ProductId) ? item.ProductId : item.ProductNameAr;

    internal static decimal Max(IEnumerable<decimal> values)
    {
        var max = values.DefaultIfEmpty(0m).Max();
        return max <= 0m ? 1m : max;
    }

    internal static double Ratio(decimal value, decimal max)
        => max <= 0m ? 0d : (double)(value / max);

    internal static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "نقداً",
        PaymentMethod.Bankak => "بنكك",
        _ => method.ToString()
    };

    internal static string WeekdayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Saturday => "السبت",
        DayOfWeek.Sunday => "الأحد",
        DayOfWeek.Monday => "الاثنين",
        DayOfWeek.Tuesday => "الثلاثاء",
        DayOfWeek.Wednesday => "الأربعاء",
        DayOfWeek.Thursday => "الخميس",
        DayOfWeek.Friday => "الجمعة",
        _ => day.ToString()
    };

    internal sealed record ItemBucket(string Name, string Category, int Units, int Orders, decimal Revenue);
}
