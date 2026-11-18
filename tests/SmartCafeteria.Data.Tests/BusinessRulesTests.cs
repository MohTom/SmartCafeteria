using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Validation;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class BusinessRulesTests
{
    private static Product ValidProduct() => new()
    {
        Id = "p-1",
        NameAr = "قهوة",
        NameEn = "Coffee",
        Category = "HotDrinks",
        Price = 10,
        Inventory = 5,
        MaxInventory = 20
    };

    [Fact]
    public void Product_WithBlankArabicName_IsRejected()
    {
        var product = ValidProduct();
        product.NameAr = "   ";

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateProduct(product));
    }

    [Fact]
    public void Product_WithNegativePrice_IsRejected()
    {
        var product = ValidProduct();
        product.Price = -1;

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateProduct(product));
    }

    [Fact]
    public void Product_WithNegativeInventory_IsRejected()
    {
        var product = ValidProduct();
        product.Inventory = -5;

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateProduct(product));
    }

    [Fact]
    public void Product_WithInventoryAboveMaximum_IsRejected()
    {
        var product = ValidProduct();
        product.Inventory = 30;

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateProduct(product));
    }

    [Fact]
    public void Product_WithoutCategory_IsRejected()
    {
        var product = ValidProduct();
        product.Category = string.Empty;

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateProduct(product));
    }

    [Fact]
    public void Supplier_WithMalformedEmail_IsRejected()
    {
        var supplier = new Supplier { Id = "sup-1", Name = "مورد", Email = "not-an-email" };

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateSupplier(supplier));
    }

    [Fact]
    public void Supplier_WithValidDetails_IsAccepted()
    {
        var supplier = new Supplier { Id = "sup-1", Name = "مورد", Email = "supplier@example.com", Phone = "0912345678" };

        BusinessRules.ValidateSupplier(supplier);
    }

    [Fact]
    public void PurchaseOrder_WithoutItems_IsRejected()
    {
        var order = new PurchaseOrder { Id = "po-1", SupplierId = "sup-1" };

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidatePurchaseOrder(order));
    }

    [Fact]
    public void PurchaseOrder_WithZeroQuantity_IsRejected()
    {
        var order = new PurchaseOrder
        {
            Id = "po-1",
            SupplierId = "sup-1",
            Items = [new PurchaseOrderItem { ProductId = "p-1", Quantity = 0, UnitCost = 5 }]
        };

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidatePurchaseOrder(order));
    }

    [Fact]
    public void PurchaseOrder_WithNegativeUnitCost_IsRejected()
    {
        var order = new PurchaseOrder
        {
            Id = "po-1",
            SupplierId = "sup-1",
            Items = [new PurchaseOrderItem { ProductId = "p-1", Quantity = 2, UnitCost = -5 }]
        };

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidatePurchaseOrder(order));
    }

    [Fact]
    public void SupplierPayment_WithNonPositiveAmount_IsRejected()
    {
        var payment = new SupplierPayment { Id = "sp-1", SupplierId = "sup-1", Amount = 0, PaymentMethod = PaymentMethod.Cash };

        Assert.Throws<ArgumentException>(() => BusinessRules.ValidateSupplierPayment(payment));
    }
}
