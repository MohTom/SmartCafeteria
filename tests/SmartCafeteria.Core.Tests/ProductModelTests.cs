using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class ProductModelTests
{
    [Fact]
    public void Product_DefaultValues_AreCorrect()
    {
        var product = new Product();
        Assert.Equal(string.Empty, product.Id);
        Assert.Equal(string.Empty, product.NameAr);
        Assert.Equal(string.Empty, product.NameEn);
        Assert.Equal(0, product.Price);
        Assert.Equal(0, product.Inventory);
        Assert.Equal(0, product.MaxInventory);
        Assert.NotNull(product.CustomizationOptions);
        Assert.Empty(product.CustomizationOptions);
    }

    [Fact]
    public void Product_CanSetAllProperties()
    {
        var product = new Product
        {
            Id = "p1",
            NameAr = "قهوة",
            NameEn = "Coffee",
            Price = 15.5m,
            Category = "HotDrinks",
            Inventory = 20,
            MaxInventory = 50
        };

        Assert.Equal("p1", product.Id);
        Assert.Equal("قهوة", product.NameAr);
        Assert.Equal("Coffee", product.NameEn);
        Assert.Equal(15.5m, product.Price);
        Assert.Equal("HotDrinks", product.Category);
        Assert.Equal(20, product.Inventory);
        Assert.Equal(50, product.MaxInventory);
    }

    [Fact]
    public void Product_AddCustomizations()
    {
        var product = new Product
        {
            Id = "p1", NameAr = "قهوة", NameEn = "Coffee", Price = 10
        };
        product.CustomizationOptions.Add(new CustomizationOption
        {
            Id = "c1", NameAr = "حليب", NameEn = "Milk", Price = 2
        });

        Assert.Single(product.CustomizationOptions);
        Assert.Equal("حليب", product.CustomizationOptions[0].NameAr);
        Assert.Equal(2, product.CustomizationOptions[0].Price);
    }
}