using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class ReportServiceTests
{
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _orderServiceMock = new Mock<IOrderService>();
        _productServiceMock = new Mock<IProductService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _service = new ReportService(_orderServiceMock.Object, _productServiceMock.Object, _settingsServiceMock.Object);
    }

    [Fact]
    public async Task GetCompletedOrdersAsync_ReturnsOnlyReceived()
    {
        var orders = new List<Order>
        {
            new Order { Id = "o1", Status = OrderStatus.Received },
            new Order { Id = "o2", Status = OrderStatus.Received },
            new Order { Id = "o3", Status = OrderStatus.Cancelled }
        };
        _orderServiceMock.Setup(s => s.GetAllOrdersAsync()).ReturnsAsync(orders);

        var result = await _service.GetCompletedOrdersAsync();

        Assert.Equal(2, result.Count());
        Assert.All(result, o => Assert.Equal(OrderStatus.Received, o.Status));
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesStats()
    {
        var settings = new AppSettings { LowStockThreshold = 5 };
        var stats = new DashboardStats { TotalRevenue = 500, CompletedOrders = 10, LowStockCount = 1 };
        _settingsServiceMock.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);
        _orderServiceMock.Setup(s => s.GetDashboardStatsAsync(5)).ReturnsAsync(stats);

        var result = await _service.GetSummaryAsync();

        Assert.Equal(500, result.revenue);
        Assert.Equal(10, result.orderCount);
        Assert.Equal(1, result.lowStockCount);
    }

    [Fact]
    public async Task GetOrdersByDateRangeAsync_PassesThrough()
    {
        var start = DateTime.Now.AddDays(-7);
        var end = DateTime.Now;
        var orders = new List<Order> { new Order { Id = "o1" } };
        _orderServiceMock.Setup(s => s.GetOrdersByDateRangeAsync(start, end)).ReturnsAsync(orders);

        var result = await _service.GetOrdersByDateRangeAsync(start, end);

        Assert.Single(result);
    }
}