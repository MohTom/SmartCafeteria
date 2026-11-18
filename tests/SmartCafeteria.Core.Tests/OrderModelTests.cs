using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using Xunit;

namespace SmartCafeteria.Core.Tests;

public class OrderModelTests
{
    [Fact]
    public void Order_DefaultValues_AreCorrect()
    {
        var order = new Order();
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal("paid", order.PaymentStatus);
        Assert.False(order.IsPreOrder);
        Assert.NotNull(order.Items);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void Order_CanSetTotalProperty()
    {
        var order = new Order
        {
            Subtotal = 50,
            CustomizationsTotal = 10,
            Total = 60
        };
        Assert.Equal(50, order.Subtotal);
        Assert.Equal(10, order.CustomizationsTotal);
        Assert.Equal(60, order.Total);
    }

    [Fact]
    public void Order_AddItems()
    {
        var order = new Order { Id = "ord-1", Code = "B-123" };
        order.Items.Add(new OrderItem
        {
            Id = "oi-1",
            OrderId = "ord-1",
            ProductId = "p1",
            ProductNameAr = "قهوة",
            Quantity = 2,
            ProductPrice = 10
        });

        Assert.Single(order.Items);
        Assert.Equal(2, order.Items[0].Quantity);
        Assert.Equal("قهوة", order.Items[0].ProductNameAr);
    }

    [Fact]
    public void Order_AllStatusValuesValid()
    {
        Assert.Equal(2, Enum.GetValues<OrderStatus>().Length);
        Assert.True(Enum.IsDefined(OrderStatus.Received));
        Assert.True(Enum.IsDefined(OrderStatus.Cancelled));
    }
}