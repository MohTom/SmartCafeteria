using SmartCafeteria.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class PurchaseOrder
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<PurchaseOrderItem> Items { get; set; } = new();
}