using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class OrderManagementServiceTests
{
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<ILogService> _logServiceMock;
    private readonly OrderManagementService _service;

    public OrderManagementServiceTests()
    {
        _orderServiceMock = new Mock<IOrderService>();
        _productServiceMock = new Mock<IProductService>();
        _logServiceMock = new Mock<ILogService>();
        _service = new OrderManagementService(_orderServiceMock.Object, _productServiceMock.Object, _logServiceMock.Object);
    }

    [Fact]
    public async Task PlaceOrderAsync_CreatesOrder_AndLogs()
    {
        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                Product = new Product { Id = "p1", NameAr = "قهوة", Price = 10 },
                Quantity = 1
            }
        };

        var expectedOrder = new Order
        {
            Id = "ord-1",
            Code = "B-123",
            Total = 10,
            Status = OrderStatus.Received
        };

        _orderServiceMock.Setup(s => s.CreateOrderAsync(It.IsAny<List<CartItem>>(), PaymentMethod.Cash, "STATION-01", "أحمد", false, null))
            .ReturnsAsync(expectedOrder);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _service.PlaceOrderAsync(cartItems, PaymentMethod.Cash, "STATION-01", "أحمد", false, null);

        Assert.Equal("ord-1", result.Id);
        Assert.Equal("B-123", result.Code);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "success"), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_ForwardsTheOrderTypeAndTheNote()
    {
        var cartItems = new List<CartItem>
        {
            new CartItem { Product = new Product { Id = "p1", NameAr = "قهوة", Price = 10 }, Quantity = 1 }
        };

        var expectedOrder = new Order { Id = "ord-2", Code = "B-124", Total = 10, OrderType = OrderType.Takeaway, Notes = "بدون سكر" };

        _orderServiceMock.Setup(s => s.CreateOrderAsync(
                It.IsAny<List<CartItem>>(), PaymentMethod.Cash, "STATION-01", "أحمد", false, null, OrderType.Takeaway, "بدون سكر"))
            .ReturnsAsync(expectedOrder);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _service.PlaceOrderAsync(
            cartItems, PaymentMethod.Cash, "STATION-01", "أحمد", false, null, OrderType.Takeaway, "بدون سكر");

        Assert.Equal(OrderType.Takeaway, result.OrderType);
        Assert.Equal("بدون سكر", result.Notes);
        _orderServiceMock.Verify(s => s.CreateOrderAsync(
            It.IsAny<List<CartItem>>(), PaymentMethod.Cash, "STATION-01", "أحمد", false, null, OrderType.Takeaway, "بدون سكر"), Times.Once);
    }

    [Fact]
    public async Task ChangeOrderStatusAsync_UpdatesStatus_AndLogs()
    {
        _orderServiceMock.Setup(s => s.UpdateOrderStatusAsync("ord-1", OrderStatus.Cancelled))
            .ReturnsAsync(true);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _service.ChangeOrderStatusAsync("ord-1", OrderStatus.Cancelled);

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "info"), Times.Once);
    }

    [Fact]
    public async Task ChangeOrderStatusAsync_NonexistentOrder_ReturnsFalse()
    {
        _orderServiceMock.Setup(s => s.UpdateOrderStatusAsync(It.IsAny<string>(), It.IsAny<OrderStatus>()))
            .ReturnsAsync(false);

        var result = await _service.ChangeOrderStatusAsync("nonexistent", OrderStatus.Received);

        Assert.False(result);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsStats()
    {
        var stats = new DashboardStats { TotalRevenue = 1000, CompletedOrders = 5, LowStockCount = 2 };
        _orderServiceMock.Setup(s => s.GetDashboardStatsAsync(5)).ReturnsAsync(stats);

        var result = await _service.GetDashboardStatsAsync(5);

        Assert.Equal(1000, result.TotalRevenue);
        Assert.Equal(5, result.CompletedOrders);
        Assert.Equal(2, result.LowStockCount);
    }
}