using System.ComponentModel.DataAnnotations;

namespace SmartCafeteria.Core.Models;
public class Product
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public decimal Price { get; set; }
    /// <summary>Category key referencing <see cref="Category.Id"/> (e.g. "HotDrinks").</summary>
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public int Inventory { get; set; }
    public int MaxInventory { get; set; }
    public string? Barcode { get; set; }
    public List<CustomizationOption> CustomizationOptions { get; set; } = new();

    /// <summary>Pixel width of the stock bar (card inner width ~248px).</summary>
    public double InventoryPercentWidth => MaxInventory > 0
        ? Math.Clamp((double)Inventory / MaxInventory, 0, 1) * 248
        : 0;
}
