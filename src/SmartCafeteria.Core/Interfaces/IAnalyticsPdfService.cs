using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Core.Interfaces;

/// <summary>Exports the analytical reports as PDF files. It is separate from
/// <see cref="IPdfService"/> so the existing reports keep their own service
/// and stay untouched.</summary>
public interface IAnalyticsPdfService
{
    /// <summary>Exports one report of the bundle and returns the file path.</summary>
    Task<string> GenerateAnalyticsReportAsync(AnalyticsBundle bundle, AnalyticsReportKind kind, AppSettings settings);

    /// <summary>Exports every report of the bundle in one document and returns
    /// the file path.</summary>
    Task<string> GenerateFullAnalyticsReportAsync(AnalyticsBundle bundle, AppSettings settings);

    /// <summary>Exports the general financial report and returns the file path.</summary>
    Task<string> GenerateFinancialReportAsync(FinancialReport report, AppSettings settings);
}
