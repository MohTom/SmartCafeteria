using SmartCafeteria.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class Order
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal CustomizationsTotal { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string PaymentStatus { get; set; } = "paid";
    public OrderStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
    public string StationId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public bool IsPreOrder { get; set; }
    public string? PickupTime { get; set; }
    /// <summary>محلي (eaten in) or سفري (takeaway). Defaults to محلي for every
    /// order created before the field existed.</summary>
    public OrderType OrderType { get; set; } = OrderType.Local;
    /// <summary>Free-text note for the whole order.</summary>
    public string Notes { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
}
