using System.Text.Json;
using Cafeteria.Contracts;
using Cafeteria.RemoteAccess;
using Moq;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class DesktopCommandHandlerTests
{
    [Fact]
    public async Task DashboardSnapshot_ReturnsDesktopStats()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new AppSettings
        {
            LowStockThreshold = 5,
            CurrencyEn = "SDG"
        });

        var orders = new Mock<IOrderService>();
        orders.Setup(s => s.GetDashboardStatsAsync(5)).ReturnsAsync(new DashboardStats
        {
            TotalRevenue = 1250.5m,
            CompletedOrders = 7,
            LowStockCount = 2,
            TotalProducts = 20
        });

        var handler = CreateHandler(settings: settings, orders: orders);
        var response = await handler.ExecuteAsync(Command("dashboard.snapshot"));

        Assert.True(response.Success);
        using var document = JsonDocument.Parse(response.ResultJson!);
        Assert.Equal(1250.5m, document.RootElement.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(7, document.RootElement.GetProperty("completedOrders").GetInt32());
        Assert.Equal("SDG", document.RootElement.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task LockUser_CallsSetActiveFalse()
    {
        var users = new Mock<IUserService>();
        users.Setup(s => s.SetActiveAsync("user-1", false)).ReturnsAsync(true);

        var handler = CreateHandler(users: users);
        var response = await handler.ExecuteAsync(Command(
            "users.lock",
            """{"userId":"user-1"}"""));

        Assert.True(response.Success);
        users.Verify(s => s.SetActiveAsync("user-1", false), Times.Once);
    }

    [Fact]
    public async Task AdjustInventory_AddsStockWithoutGoingNegative()
    {
        var product = new Product { Id = "p1", NameAr = "شاي", NameEn = "Tea", Inventory = 10 };
        var products = new Mock<IProductService>();
        products.Setup(s => s.GetProductByIdAsync("p1")).ReturnsAsync(product);
        products.Setup(s => s.UpdateInventoryAsync("p1", -5)).ReturnsAsync(true);

        var handler = CreateHandler(products: products);
        var response = await handler.ExecuteAsync(Command(
            "inventory.adjust",
            """{"productId":"p1","delta":5}"""));

        Assert.True(response.Success);
        products.Verify(s => s.UpdateInventoryAsync("p1", -5), Times.Once);

        using var document = JsonDocument.Parse(response.ResultJson!);
        Assert.Equal(15, document.RootElement.GetProperty("inventory").GetInt32());
    }

    [Fact]
    public async Task AdjustInventory_RejectsNegativeResult()
    {
        var product = new Product { Id = "p1", Inventory = 2 };
        var products = new Mock<IProductService>();
        products.Setup(s => s.GetProductByIdAsync("p1")).ReturnsAsync(product);

        var handler = CreateHandler(products: products);
        var response = await handler.ExecuteAsync(Command(
            "inventory.adjust",
            """{"productId":"p1","delta":-5}"""));

        Assert.False(response.Success);
        Assert.Contains("Insufficient stock", response.Error);
        products.Verify(s => s.UpdateInventoryAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UnknownCommand_IsRejected()
    {
        var handler = CreateHandler();
        var response = await handler.ExecuteAsync(Command("sql.execute"));

        Assert.False(response.Success);
        Assert.Contains("not supported", response.Error);
    }

    private static DesktopCommandHandler CreateHandler(
        Mock<IUserService>? users = null,
        Mock<IProductService>? products = null,
        Mock<IOrderService>? orders = null,
        Mock<ISettingsService>? settings = null)
    {
        return new DesktopCommandHandler(
            users?.Object ?? Mock.Of<IUserService>(),
            products?.Object ?? Mock.Of<IProductService>(),
            orders?.Object ?? Mock.Of<IOrderService>(),
            settings?.Object ?? Mock.Of<ISettingsService>(),
            Mock.Of<IAnalyticsService>());
    }

    private static CommandExecutionRequest Command(string type, string payload = "{}") =>
        new(Guid.NewGuid(), type, payload, Guid.NewGuid(), "Remote administrator");
}
