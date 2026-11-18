using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly DatabaseInitializer _initializer;

    public DatabaseIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_test_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        _initializer = new DatabaseInitializer(_dbContext);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task ProductRepository_AddAndGetAll()
    {
        var repo = new ProductRepository(_dbContext);
        var product = new Product
        {
            Id = "test-p1",
            NameAr = "قهوة",
            NameEn = "Coffee",
            Price = 20,
            Category = "HotDrinks",
            Inventory = 10,
            MaxInventory = 50
        };
        product.CustomizationOptions.Add(new CustomizationOption
        {
            Id = "test-c1",
            NameAr = "سكر",
            NameEn = "Sugar",
            Price = 1
        });

        await repo.AddProductAsync(product);
        var all = await repo.GetAllProductsAsync();

        var retrieved = Assert.Single(all, p => p.Id == "test-p1");
        Assert.Equal("قهوة", retrieved.NameAr);
        Assert.Single(retrieved.CustomizationOptions);
    }

    [Fact]
    public async Task ProductRepository_GetById_ReturnsCustomizations()
    {
        var repo = new ProductRepository(_dbContext);
        var product = new Product { Id = "p2", NameAr = "شاي", NameEn = "Tea", Price = 5, Category = "HotDrinks" };
        product.CustomizationOptions.Add(new CustomizationOption { Id = "c2", NameAr = "ليمون", NameEn = "Lemon", Price = 0 });
        await repo.AddProductAsync(product);

        var result = await repo.GetProductByIdAsync("p2");
        Assert.NotNull(result);
        Assert.Single(result.CustomizationOptions);
        Assert.Equal("ليمون", result.CustomizationOptions[0].NameAr);
    }

    [Fact]
    public async Task ProductRepository_Delete_RemovesProduct()
    {
        var repo = new ProductRepository(_dbContext);
        await repo.AddProductAsync(new Product { Id = "p-del", NameAr = "عصير", NameEn = "Juice", Price = 10, Category = "HotDrinks" });
        var deleted = await repo.DeleteProductAsync("p-del");
        Assert.True(deleted);
        var result = await repo.GetProductByIdAsync("p-del");
        Assert.Null(result);
    }

    [Fact]
    public async Task ProductRepository_UpdateInventory_DecrementsStock()
    {
        var repo = new ProductRepository(_dbContext);
        await repo.AddProductAsync(new Product { Id = "p-inv", NameAr = "حلوى", NameEn = "Sweet", Price = 3, Category = "HotDrinks", Inventory = 10 });
        var result = await repo.UpdateInventoryAsync("p-inv", 3);
        Assert.True(result);

        var updated = await repo.GetProductByIdAsync("p-inv");
        Assert.NotNull(updated);
        Assert.Equal(7, updated.Inventory);
    }

    [Fact]
    public async Task OrderRepository_CreateOrder_ComputesTotals()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-total", NameAr = "فطور", NameEn = "Breakfast", Price = 30, Category = "HotDrinks", Inventory = 100, MaxInventory = 100 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                Product = new Product { Id = "p-total", NameAr = "فطور", Price = 30, Category = "HotDrinks" },
                Quantity = 2
            }
        };

        var order = await orderRepo.CreateOrderAsync(cartItems, PaymentMethod.Cash, "T1", "أحمد", false, null);

        Assert.Equal(60, order.Total);
        Assert.Equal("paid", order.PaymentStatus);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task OrderRepository_CreateOrder_PersistsTheOrderTypeAndTheNote()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-type", NameAr = "فطور", NameEn = "Breakfast", Price = 30, Category = "HotDrinks", Inventory = 100 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem { Product = new Product { Id = "p-type", NameAr = "فطور", Price = 30, Category = "HotDrinks" }, Quantity = 1 }
        };

        var order = await orderRepo.CreateOrderAsync(
            cartItems, PaymentMethod.Cash, "T1", "أحمد", false, null, OrderType.Takeaway, "بدون بصل");

        Assert.Equal(OrderType.Takeaway, order.OrderType);
        Assert.Equal("بدون بصل", order.Notes);

        var reloaded = await orderRepo.GetOrderByIdAsync(order.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderType.Takeaway, reloaded.OrderType);
        Assert.Equal("بدون بصل", reloaded.Notes);
    }

    [Fact]
    public async Task OrderRepository_CreateOrder_DefaultsToLocalWithoutANote()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-default", NameAr = "قهوة", NameEn = "Coffee", Price = 10, Category = "HotDrinks", Inventory = 10 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem { Product = new Product { Id = "p-default", NameAr = "قهوة", Price = 10, Category = "HotDrinks" }, Quantity = 1 }
        };

        var order = await orderRepo.CreateOrderAsync(cartItems, PaymentMethod.Cash, "T1", "أحمد", false, null);

        var reloaded = await orderRepo.GetOrderByIdAsync(order.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderType.Local, reloaded.OrderType);
        Assert.Equal(string.Empty, reloaded.Notes);
    }

    [Fact]
    public async Task OrderRepository_CreateOrder_DecrementsInventory()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-dec", NameAr = "شراب", NameEn = "Drink", Price = 5, Category = "HotDrinks", Inventory = 10 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem { Product = new Product { Id = "p-dec", NameAr = "شراب", Price = 5, Category = "HotDrinks" }, Quantity = 4 }
        };

        await orderRepo.CreateOrderAsync(cartItems, PaymentMethod.Bankak, "T1", "أحمد", false, null);
        var product = await productRepo.GetProductByIdAsync("p-dec");
        Assert.NotNull(product);
        Assert.Equal(6, product.Inventory);
    }

    [Fact]
    public async Task OrderRepository_UpdateStatus()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-st", NameAr = "مخبوز", NameEn = "Baked", Price = 8, Category = "HotDrinks", Inventory = 10 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { new CartItem { Product = new Product { Id = "p-st", NameAr = "مخبوز", Price = 8, Category = "HotDrinks" }, Quantity = 1 } },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        await orderRepo.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);
        var updated = await orderRepo.GetOrderByIdAsync(order.Id);
        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task SettingsRepository_GetDefault_ReturnsDefaults()
    {
        var repo = new SettingsRepository(_dbContext);
        var settings = await repo.GetSettingsAsync();
        Assert.Equal("كافتيريا التميز", settings.CafeteriaNameAr);
        Assert.Equal("ج.س", settings.CurrencyAr);
    }

    [Fact]
    public async Task SettingsRepository_SaveAndGet()
    {
        var repo = new SettingsRepository(_dbContext);
        var settings = new AppSettings
        {
            CafeteriaNameAr = "كافيه جديد",
            PrintMethod = PrintMethod.TotalReceiptTwice,
            LowStockThreshold = 3
        };
        await repo.SaveSettingsAsync(settings);

        var result = await repo.GetSettingsAsync();
        Assert.Equal("كافيه جديد", result.CafeteriaNameAr);
        Assert.Equal(PrintMethod.TotalReceiptTwice, result.PrintMethod);
        Assert.Equal(3, result.LowStockThreshold);
    }

    [Fact]
    public async Task LogRepository_AddAndGetLogs()
    {
        var repo = new LogRepository(_dbContext);
        await repo.AddLogAsync("اختبار سجل", "info");
        var logs = await repo.GetAllLogsAsync();
        var added = Assert.Single(logs, l => l.Message == "اختبار سجل");
        Assert.Equal("info", added.Level);
    }

    [Fact]
    public async Task OrderRepository_CreateOrder_AssignsDailySequentialNumbers()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-seq", NameAr = "قهوة", NameEn = "Coffee", Price = 10, Category = "HotDrinks", Inventory = 100 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        CartItem Item() => new() { Product = new Product { Id = "p-seq", NameAr = "قهوة", Price = 10, Category = "HotDrinks" }, Quantity = 1 };

        var first = await orderRepo.CreateOrderAsync(new List<CartItem> { Item() }, PaymentMethod.Cash, "T1", "أحمد", false, null);
        var second = await orderRepo.CreateOrderAsync(new List<CartItem> { Item() }, PaymentMethod.Cash, "T1", "أحمد", false, null);

        Assert.Equal("1", first.Code);
        Assert.Equal("2", second.Code);
    }

    [Fact]
    public async Task OrderRepository_Delete_RemovesOrderAndRestoresStock()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-del-ord", NameAr = "قهوة", NameEn = "Coffee", Price = 10, Category = "HotDrinks", Inventory = 10 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { new CartItem { Product = new Product { Id = "p-del-ord", NameAr = "قهوة", Price = 10, Category = "HotDrinks" }, Quantity = 3 } },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        Assert.True(await orderRepo.DeleteOrderAsync(order.Id));
        Assert.Null(await orderRepo.GetOrderByIdAsync(order.Id));

        var product = await productRepo.GetProductByIdAsync("p-del-ord");
        Assert.NotNull(product);
        Assert.Equal(10, product.Inventory);
    }

    [Fact]
    public async Task OrderRepository_DeleteCancelled_DoesNotRestoreStockTwice()
    {
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product { Id = "p-del-can", NameAr = "شاي", NameEn = "Tea", Price = 5, Category = "HotDrinks", Inventory = 10 });

        var orderRepo = new OrderRepository(_dbContext, productRepo);
        var order = await orderRepo.CreateOrderAsync(
            new List<CartItem> { new CartItem { Product = new Product { Id = "p-del-can", NameAr = "شاي", Price = 5, Category = "HotDrinks" }, Quantity = 2 } },
            PaymentMethod.Cash, "T1", "أحمد", false, null);

        await orderRepo.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);
        Assert.True(await orderRepo.DeleteOrderAsync(order.Id));
        Assert.Null(await orderRepo.GetOrderByIdAsync(order.Id));

        var product = await productRepo.GetProductByIdAsync("p-del-can");
        Assert.NotNull(product);
        Assert.Equal(10, product.Inventory);
    }

    [Fact]
    public async Task OrderRepository_DeleteMissing_ReturnsFalse()
    {
        var orderRepo = new OrderRepository(_dbContext, new ProductRepository(_dbContext));
        Assert.False(await orderRepo.DeleteOrderAsync("ord-missing"));
    }

    [Fact]
    public async Task LogRepository_ClearLogs()
    {
        var repo = new LogRepository(_dbContext);
        await repo.AddLogAsync("رسالة", "info");
        await repo.ClearLogsAsync();
        var logs = await repo.GetAllLogsAsync();
        Assert.Empty(logs);
    }
}