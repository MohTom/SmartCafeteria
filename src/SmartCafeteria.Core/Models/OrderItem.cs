namespace SmartCafeteria.Core.Models;
public class OrderItem
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal CustomizationsTotal { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string SelectedCustomizationsJson { get; set; } = "[]";
}
