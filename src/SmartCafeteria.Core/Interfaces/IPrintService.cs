using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Interfaces;
public interface IPrintService
{
    Task PrintReceiptAsync(Order order, AppSettings settings);
    /// <summary>Prints the short cashier copy of a double receipt.</summary>
    Task PrintCashierSummaryAsync(Order order, AppSettings settings);
    Task PrintKitchenReceiptAsync(Order order, string sectionName, List<OrderItem> sectionItems, AppSettings settings, bool minimal = false);
    Task<List<string>> GetInstalledPrintersAsync();
    Task<string> GetDefaultPrinterAsync();
}
