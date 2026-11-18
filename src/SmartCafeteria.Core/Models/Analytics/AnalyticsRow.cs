namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>One line of an analytical report. Every report uses the same four
/// text slots so a single table template can render any report without the
/// view knowing the shape of the underlying data.</summary>
public sealed class AnalyticsRow
{
    /// <summary>1-based position in the report, empty when the report is not ranked.</summary>
    public string Rank { get; init; } = string.Empty;

    /// <summary>Main label of the row, for example the item or category name.</summary>
    public string Primary { get; init; } = string.Empty;

    /// <summary>Supporting label, for example the category of the item.</summary>
    public string Secondary { get; init; } = string.Empty;

    /// <summary>Second supporting label, for example the quantity sold.</summary>
    public string Tertiary { get; init; } = string.Empty;

    /// <summary>The measured value of the row, for example the revenue.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Share of the largest value in the same report, between 0 and 1.
    /// Drives the inline bar next to the row.</summary>
    public double BarRatio { get; init; }
}
