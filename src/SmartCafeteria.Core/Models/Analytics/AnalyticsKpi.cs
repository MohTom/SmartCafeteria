namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>A headline figure of the analytics section, shown as a card.</summary>
public sealed class AnalyticsKpi
{
    /// <summary>Arabic label of the figure.</summary>
    public required string Label { get; init; }

    /// <summary>Formatted value of the figure.</summary>
    public required string Value { get; init; }
}
