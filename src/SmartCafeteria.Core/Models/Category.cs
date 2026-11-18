namespace SmartCafeteria.Core.Models;

/// <summary>Product category, managed from the Settings screen.</summary>
public class Category
{
    /// <summary>Stable key (e.g. "HotDrinks"). Referenced by <see cref="Product.Category"/>.</summary>
    public string Id { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
