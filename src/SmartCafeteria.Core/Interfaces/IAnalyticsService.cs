using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Core.Interfaces;

/// <summary>Builds the analytical reports of the reports screen from the data
/// that already exists in the database. The service only reads: it never
/// creates, updates or deletes a record.</summary>
public interface IAnalyticsService
{
    /// <summary>Reads the source data once and returns every analytical report
    /// for the given range. A null end date means the range is open ended.</summary>
    Task<AnalyticsBundle> BuildAsync(DateTime? startDate, DateTime? endDate);
}
