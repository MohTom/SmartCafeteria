namespace SmartCafeteria.Core.Models;
public class CustomizationOption
{
    public string Id { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
