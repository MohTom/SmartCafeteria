using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class InventoryTrackingIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly DatabaseInitializer _initializer;

    public InventoryTrackingIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_inventory_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        _initializer = new DatabaseInitializer(_dbContext);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task CreateOrder_WithZeroStock_Succeeds_WhenTrackingIsDisabled()
    {
        var productRepo = await SeedProductAsync("p-zero", inventory: 0);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = false };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-zero", quantity: 1) },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        var product = await productRepo.GetProductByIdAsync("p-zero");
        Assert.NotNull(product);
        Assert.Equal(0, product.Inventory);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task CreateOrder_WithZeroStock_Throws_WhenTrackingIsEnabled()
    {
        var productRepo = await SeedProductAsync("p-zero", inventory: 0);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = true };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-zero", quantity: 1) },
            PaymentMethod.Cash, "T1", "أحمد", false, null));

        Assert.Empty(await orderRepo.GetAllOrdersAsync());
    }

    [Fact]
    public async Task CreateOrder_DoesNotDecrementStock_WhenTrackingIsDisabled()
    {
        var productRepo = await SeedProductAsync("p-stock", inventory: 10);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = false };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        await orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-stock", quantity: 3) },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        var product = await productRepo.GetProductByIdAsync("p-stock");
        Assert.NotNull(product);
        Assert.Equal(10, product.Inventory);
    }

    [Fact]
    public async Task CancelOrder_RestoresStock_WhenTrackingIsEnabled()
    {
        var productRepo = await SeedProductAsync("p-cancel", inventory: 10);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = true };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-cancel", quantity: 2) },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        await orderRepo.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        var product = await productRepo.GetProductByIdAsync("p-cancel");
        Assert.NotNull(product);
        Assert.Equal(10, product.Inventory);
    }

    [Fact]
    public async Task CancelOrder_DoesNotChangeStock_WhenTrackingIsDisabled()
    {
        var productRepo = await SeedProductAsync("p-cancel-off", inventory: 0);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = false };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-cancel-off", quantity: 2) },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        await orderRepo.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        var product = await productRepo.GetProductByIdAsync("p-cancel-off");
        Assert.NotNull(product);
        Assert.Equal(0, product.Inventory);
    }

    [Fact]
    public async Task DeleteOrder_DoesNotChangeStock_WhenTrackingIsDisabled()
    {
        var productRepo = await SeedProductAsync("p-delete-off", inventory: 0);
        var settings = new FakeTicketConfigStore { EnforceInventoryTracking = false };
        var orderRepo = new OrderRepository(_dbContext, productRepo, settings);

        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { Item("p-delete-off", quantity: 2) },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        await orderRepo.DeleteOrderAsync(order.Id);

        var product = await productRepo.GetProductByIdAsync("p-delete-off");
        Assert.NotNull(product);
        Assert.Equal(0, product.Inventory);
    }

    private async Task<ProductRepository> SeedProductAsync(string id, int inventory)
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product
        {
            Id = id,
            NameAr = "منتج",
            NameEn = "Product",
            Price = 10,
            Category = "HotDrinks",
            Inventory = inventory,
            MaxInventory = 100
        });
        return productRepo;
    }

    private static CartItem Item(string productId, int quantity) => new()
    {
        Product = new Product
        {
            Id = productId,
            NameAr = "منتج",
            NameEn = "Product",
            Price = 10,
            Category = "HotDrinks"
        },
        Quantity = quantity
    };

    private sealed class FakeTicketConfigStore : ITicketConfigStore
    {
        public bool EnforceInventoryTracking { get; set; } = true;
        public string ConfigPath => string.Empty;
        public TicketPrintConfig Load() => new() { EnforceInventoryTracking = EnforceInventoryTracking };
        public void Save(TicketPrintConfig config) => EnforceInventoryTracking = config.EnforceInventoryTracking;
    }
}
