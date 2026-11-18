using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Models.Analytics;

/// <summary>One report of the analytics section: a title, the headers of its
/// four value slots and the rows themselves.</summary>
public sealed class AnalyticsReport
{
    /// <summary>The kind of report, used to address it from the UI and the exporter.</summary>
    public required AnalyticsReportKind Kind { get; init; }

    /// <summary>Arabic title shown above the table.</summary>
    public required string Title { get; init; }

    /// <summary>Headers of the four value slots, in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>The rows of the report, already sorted and formatted.</summary>
    public required IReadOnlyList<AnalyticsRow> Rows { get; init; }

    /// <summary>Message shown when the report has no rows.</summary>
    public string EmptyMessage { get; init; } = "لا توجد بيانات في النطاق المحدد";
}
