using SmartCafeteria.Core.Models;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class CartItemTests
{
    [Fact]
    public void CartItem_LineTotal_CalculatesCorrectly()
    {
        var product = new Product { Price = 10 };
        var item = new CartItem
        {
            Product = product,
            Quantity = 2
        };
        item.SelectedCustomizations.Add(new CustomizationOption { Price = 3 });

        Assert.Equal(26, item.LineTotal);
    }

    [Fact]
    public void CartItem_NoCustomizations_LineTotalIsBasePrice()
    {
        var product = new Product { Price = 15 };
        var item = new CartItem
        {
            Product = product,
            Quantity = 3
        };
        Assert.Equal(45, item.LineTotal);
    }

    [Fact]
    public void CartItem_Quantity_DefaultIsOne()
    {
        var item = new CartItem();
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public void CartItem_QuantityChange_RaisesPropertyChanged()
    {
        var item = new CartItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Quantity = 5;

        Assert.Contains(nameof(CartItem.Quantity), raised);
        Assert.Contains(nameof(CartItem.LineTotal), raised);
    }
}