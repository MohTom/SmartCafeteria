using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Core.Models;
public class AppSettings
{
    public int Id { get; set; } = 1;
    public string CurrencyAr { get; set; } = "ج.س";
    public string CurrencyEn { get; set; } = "SDG";
    public int LowStockThreshold { get; set; } = 5;
    public string CashierName { get; set; } = "أحمد السوداني";
    public string StationId { get; set; } = "TERMINAL-01";
    public string PrinterName { get; set; } = string.Empty;
    public PrintMethod PrintMethod { get; set; } = PrintMethod.FullReceiptAndSectionReceipts;
    public string CafeteriaNameAr { get; set; } = "كافتيريا التميز";
    public string CafeteriaNameEn { get; set; } = "AL-TAMAYUZ CAFETERIA";
    public string CafeteriaAddress { get; set; } = "شارع النيل الخرطوم";
    public string CafeteriaPhone { get; set; } = "+249 123 456 789";
}
