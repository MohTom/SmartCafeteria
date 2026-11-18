namespace SmartCafeteria.Core.Models;
public class PurchaseOrderItem
{
    public string Id { get; set; } = string.Empty;
    public string PurchaseOrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Total => Quantity * UnitCost;
}