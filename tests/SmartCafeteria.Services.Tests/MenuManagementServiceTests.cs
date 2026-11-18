using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class MenuManagementServiceTests
{
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<ILogService> _logServiceMock;
    private readonly MenuManagementService _service;

    public MenuManagementServiceTests()
    {
        _productServiceMock = new Mock<IProductService>();
        _logServiceMock = new Mock<ILogService>();
        _service = new MenuManagementService(_productServiceMock.Object, _logServiceMock.Object);
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsAllProducts()
    {
        var products = new List<Product>
        {
            new Product { Id = "p1", NameAr = "قهوة" },
            new Product { Id = "p2", NameAr = "شاي" }
        };
        _productServiceMock.Setup(s => s.GetAllProductsAsync()).ReturnsAsync(products);

        var result = await _service.GetAllProductsAsync();

        Assert.Equal(2, result.Count());
        Assert.Contains(result, p => p.NameAr == "قهوة");
    }

    [Fact]
    public async Task AddProductAsync_AddsAndLogs()
    {
        var product = new Product { Id = "p-new", NameAr = "عصير", Price = 10 };
        _productServiceMock.Setup(s => s.AddProductAsync(product)).ReturnsAsync(true);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.AddProductAsync(product);

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "success"), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_Deletes_AndLogs()
    {
        _productServiceMock.Setup(s => s.DeleteProductAsync("p1")).ReturnsAsync(true);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.DeleteProductAsync("p1");

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "warning"), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_Updates_AndLogs()
    {
        var product = new Product { Id = "p1", NameAr = "قهوة", Price = 12 };
        _productServiceMock.Setup(s => s.UpdateProductAsync(product)).ReturnsAsync(true);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.UpdateProductAsync(product);

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "info"), Times.Once);
    }

    [Fact]
    public async Task RestockProductAsync_Restocks_AndLogs()
    {
        _productServiceMock.Setup(s => s.RestockProductAsync("p1")).ReturnsAsync(true);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.RestockProductAsync("p1");

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "success"), Times.Once);
    }

    [Fact]
    public async Task CheckLowStockAsync_DetectsLowAndZeroStock()
    {
        var products = new List<Product>
        {
            new Product { Id = "p1", NameAr = "قهوة", Inventory = 2 },
            new Product { Id = "p2", NameAr = "شاي", Inventory = 50 },
            new Product { Id = "p3", NameAr = "عصير", Inventory = 0 }
        };
        _productServiceMock.Setup(s => s.GetAllProductsAsync()).ReturnsAsync(products);
        _logServiceMock.Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.CheckLowStockAsync(5);

        Assert.True(result);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "warning"), Times.AtLeastOnce);
        _logServiceMock.Verify(s => s.AddLogAsync(It.IsAny<string>(), "alert"), Times.Once);
    }
}