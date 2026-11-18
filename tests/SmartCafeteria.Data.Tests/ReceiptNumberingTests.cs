using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class ReceiptNumberingTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly ProductRepository _productRepo;
    private readonly OrderRepository _orderRepo;

    public ReceiptNumberingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_receipt_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(_dbContext).InitializeAsync().GetAwaiter().GetResult();
        _productRepo = new ProductRepository(_dbContext);
        _orderRepo = new OrderRepository(_dbContext, _productRepo);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    private async Task<Order> CreateOrderAsync(string productId)
        => await _orderRepo.CreateOrderAsync(
            new List<CartItem>
            {
                new() { Product = new Product { Id = productId, NameAr = "صنف", Price = 10, Category = "HotDrinks" }, Quantity = 1 }
            },
            PaymentMethod.Cash, "T1", "كاشير", false, null);

    [Fact]
    public async Task ReceiptNumbers_AreSequential_PerDay()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-1", NameAr = "صنف", NameEn = "Item", Price = 10, Category = "HotDrinks", Inventory = 100, MaxInventory = 100 });

        var first = await CreateOrderAsync("p-1");
        var second = await CreateOrderAsync("p-1");

        Assert.Equal("1", first.Code);
        Assert.Equal("2", second.Code);
    }

    [Fact]
    public async Task ReceiptNumbers_AreNotReused_AfterDeletingAnOrder()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-2", NameAr = "صنف", NameEn = "Item", Price = 10, Category = "HotDrinks", Inventory = 100, MaxInventory = 100 });

        var first = await CreateOrderAsync("p-2");
        var second = await CreateOrderAsync("p-2");
        Assert.Equal("1", first.Code);
        Assert.Equal("2", second.Code);

        Assert.True(await _orderRepo.DeleteOrderAsync(second.Id));

        var third = await CreateOrderAsync("p-2");
        Assert.Equal("3", third.Code);
    }

    [Fact]
    public async Task ReceiptCounter_IsSeededFromExistingOrders_WhenMissing()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-3", NameAr = "صنف", NameEn = "Item", Price = 10, Category = "HotDrinks", Inventory = 100, MaxInventory = 100 });

        var existingOne = await CreateOrderAsync("p-3");
        var existingTwo = await CreateOrderAsync("p-3");
        Assert.Equal("1", existingOne.Code);
        Assert.Equal("2", existingTwo.Code);

        using (var conn = await _dbContext.GetConnectionAsync())
            await conn.ExecuteAsync("DELETE FROM OrderCounters");

        var next = await CreateOrderAsync("p-3");
        Assert.Equal("3", next.Code);
    }

    [Fact]
    public async Task CancelledOrder_KeepsItsNumberAndTheNextOneAdvances()
    {
        await _productRepo.AddProductAsync(new Product { Id = "p-4", NameAr = "صنف", NameEn = "Item", Price = 10, Category = "HotDrinks", Inventory = 100, MaxInventory = 100 });

        var cancelled = await CreateOrderAsync("p-4");
        Assert.True(await _orderRepo.UpdateOrderStatusAsync(cancelled.Id, OrderStatus.Cancelled));

        var next = await CreateOrderAsync("p-4");
        Assert.Equal("2", next.Code);
        Assert.NotEqual(cancelled.Code, next.Code);
    }
}
