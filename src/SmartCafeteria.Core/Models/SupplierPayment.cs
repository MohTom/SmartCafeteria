using SmartCafeteria.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class SupplierPayment
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string Notes { get; set; } = string.Empty;
}