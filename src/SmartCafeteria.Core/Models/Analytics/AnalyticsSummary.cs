namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>The figures behind the executive summary of the analytics section.
/// Kept as plain numbers so the tests can assert on them directly instead of
/// on the formatted text that is shown on screen.</summary>
public sealed class AnalyticsSummary
{
    /// <summary>Revenue of the received orders in the range.</summary>
    public decimal Revenue { get; init; }

    /// <summary>Number of received orders in the range.</summary>
    public int Orders { get; init; }

    /// <summary>Average value of one received order.</summary>
    public decimal AverageBasket { get; init; }

    /// <summary>Total number of items sold in the range.</summary>
    public int UnitsSold { get; init; }

    /// <summary>Average number of items per received order.</summary>
    public decimal ItemsPerOrder { get; init; }

    /// <summary>Total expenses recorded in the range.</summary>
    public decimal Expenses { get; init; }

    /// <summary>Income recorded outside of sales in the range.</summary>
    public decimal OtherIncome { get; init; }

    /// <summary>Revenue plus other income minus expenses.</summary>
    public decimal NetProfit { get; init; }

    /// <summary>Net profit as a percentage of the revenue.</summary>
    public decimal MarginPercent { get; init; }

    /// <summary>Number of cancelled orders in the range.</summary>
    public int CancelledOrders { get; init; }

    /// <summary>Value of the cancelled orders in the range.</summary>
    public decimal CancelledValue { get; init; }

    /// <summary>Number of products that hold stock.</summary>
    public int ActiveProducts { get; init; }

    /// <summary>Number of products that were not sold during the range.</summary>
    public int DeadStockCount { get; init; }

    /// <summary>Name of the best selling product.</summary>
    public string TopProductName { get; init; } = string.Empty;

    /// <summary>Name of the best selling category.</summary>
    public string TopCategoryName { get; init; } = string.Empty;

    /// <summary>Hour of the day with the most orders, for example "14:00 - 15:00".</summary>
    public string BusiestHourLabel { get; init; } = string.Empty;
}
