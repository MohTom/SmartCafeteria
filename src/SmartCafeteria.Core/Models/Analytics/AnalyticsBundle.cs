using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>Everything the analytics section shows, built in one read pass and
/// then shared by the on-screen tables and the PDF exports.</summary>
public sealed class AnalyticsBundle
{
    /// <summary>First day of the reported range, null when the range is open.</summary>
    public DateTime? Start { get; init; }

    /// <summary>Last moment of the reported range, null when the range is open.</summary>
    public DateTime? End { get; init; }

    /// <summary>Arabic description of the range.</summary>
    public string PeriodLabel { get; init; } = string.Empty;

    /// <summary>The figures behind the executive summary.</summary>
    public required AnalyticsSummary Summary { get; init; }

    /// <summary>The headline figures shown as cards.</summary>
    public required IReadOnlyList<AnalyticsKpi> Kpis { get; init; }

    /// <summary>Every report of the section, in display order.</summary>
    public required IReadOnlyList<AnalyticsReport> Reports { get; init; }

    /// <summary>The general financial report that lists every money movement of
    /// the period with a closing summary.</summary>
    public FinancialReport? Financial { get; init; }

    /// <summary>Finds a report by its kind, or null when the bundle does not carry it.</summary>
    public AnalyticsReport? Find(AnalyticsReportKind kind)
    {
        foreach (var report in Reports)
        {
            if (report.Kind == kind)
                return report;
        }

        return null;
    }
}
