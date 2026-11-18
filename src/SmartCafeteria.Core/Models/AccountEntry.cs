using SmartCafeteria.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class AccountEntry
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public AccountEntryType Type { get; set; }
    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}