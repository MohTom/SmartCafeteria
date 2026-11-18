namespace SmartCafeteria.Printing;

public class TicketPrintModel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    /// <summary>Section/category name, printed on its own line on minimal tickets.</summary>
    public string SectionName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    /// <summary>
    /// محلي or سفري, already in Arabic. Empty on tickets built without an order.
    /// Printed by the full, minimal and cashier-summary layouts.
    /// </summary>
    public string OrderType { get; set; } = string.Empty;
    public string PickupTime { get; set; } = string.Empty;
    public List<TicketLine> Lines { get; set; } = new();
    public decimal Total { get; set; }
    /// <summary>Minimal kitchen ticket: name, address, receipt number, section and meals only.</summary>
    public bool IsMinimal { get; set; }
    /// <summary>
    /// Cashier copy of a double receipt: the cafeteria name, the receive label,
    /// the receipt number, the order type, the meals and the total — nothing else.
    /// </summary>
    public bool IsCashierSummary { get; set; }
    /// <summary>Label printed under the cafeteria name on a cashier summary.</summary>
    public string SummaryTitle { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "SDG";
    public string CurrencySymbolAr { get; set; } = "ج.س";
    public string OrderId { get; set; } = string.Empty;
}
