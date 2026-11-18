namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>The general financial report: every money movement of the period in
/// one chronological ledger, closed by the totals of <see cref="FinancialSummary"/>.</summary>
public sealed class FinancialReport
{
    /// <summary>Arabic description of the reported period.</summary>
    public required string PeriodLabel { get; init; }

    /// <summary>Currency shown next to every amount.</summary>
    public required string Currency { get; init; }

    /// <summary>Every operation of the period, oldest first.</summary>
    public required IReadOnlyList<FinancialOperation> Operations { get; init; }

    /// <summary>The closing totals of the report.</summary>
    public required FinancialSummary Summary { get; init; }
}
