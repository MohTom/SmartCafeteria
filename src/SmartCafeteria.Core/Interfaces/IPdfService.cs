using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface IPdfService
{
    Task<string> GenerateSalesReportAsync(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate);
    Task<string> GenerateOrdersReportAsync(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate);
    Task<string> GenerateInventoryReportAsync(IEnumerable<Product> products, AppSettings settings);
    Task<string> GenerateOperationsLogReportAsync(IEnumerable<SystemLog> logs, AppSettings settings, DateTime? startDate, DateTime? endDate);
}
