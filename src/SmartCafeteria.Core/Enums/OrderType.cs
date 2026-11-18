namespace SmartCafeteria.Core.Enums;

/// <summary>How the customer wants the order: eaten in the cafeteria (محلي)
/// or taken away (سفري). The numeric value is what the database stores.</summary>
public enum OrderType
{
    Local = 0,
    Takeaway = 1
}

/// <summary>The one place that knows the Arabic label of an order type, so the
/// screen, the receipt, the PDF and the analytical reports never disagree.</summary>
public static class OrderTypeExtensions
{
    public static string ToArabic(this OrderType type) => type switch
    {
        OrderType.Local => "محلي",
        OrderType.Takeaway => "سفري",
        _ => type.ToString()
    };
}
