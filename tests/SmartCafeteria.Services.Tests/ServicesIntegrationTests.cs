using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class ServicesIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly ProductRepository _productRepo;
    private readonly OrderRepository _orderRepo;
    private readonly LogRepository _logRepo;
    private readonly SettingsRepository _settingsRepo;

    public ServicesIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_svc_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        var initializer = new DatabaseInitializer(_dbContext);
        initializer.InitializeAsync().GetAwaiter().GetResult();
        _logRepo = new LogRepository(_dbContext);
        _settingsRepo = new SettingsRepository(_dbContext);
        _productRepo = new ProductRepository(_dbContext);
        _orderRepo = new OrderRepository(_dbContext, _productRepo);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task OrderManagementService_PlaceOrder_EndToEnd()
    {
        var product = new Product
        {
            Id = "p-e2e",
            NameAr = "قهوة تركية",
            NameEn = "Turkish Coffee",
            Price = 15,
            Inventory = 20,
            MaxInventory = 50,
            Category = "HotDrinks"
        };
        product.CustomizationOptions.Add(new CustomizationOption { Id = "c-sugar", NameAr = "سكر", NameEn = "Sugar", Price = 1 });
        await _productRepo.AddProductAsync(product);

        var service = new OrderManagementService(_orderRepo, _productRepo, _logRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                Product = product,
                Quantity = 2,
                SelectedCustomizations = { product.CustomizationOptions[0] }
            }
        };

        var order = await service.PlaceOrderAsync(cartItems, PaymentMethod.Cash, "TERMINAL-01", "أحمد", false, null);

        Assert.StartsWith("ord-", order.Id);
        Assert.Equal(32, order.Total); // (15+1)*2
        Assert.Equal(OrderStatus.Received, order.Status);

        var productAfter = await _productRepo.GetProductByIdAsync("p-e2e");
        Assert.NotNull(productAfter);
        Assert.Equal(18, productAfter.Inventory);

        var logs = await _logRepo.GetAllLogsAsync();
        Assert.Contains(logs, l => l.Message.Contains(order.Code));
    }

    [Fact]
    public async Task OrderManagementService_PlaceOrder_PersistsTheOrderTypeAndTheNote()
    {
        var product = new Product { Id = "p-notes", NameAr = "ساندويتش", NameEn = "Sandwich", Price = 20, Category = "HotDrinks", Inventory = 10 };
        await _productRepo.AddProductAsync(product);

        var service = new OrderManagementService(_orderRepo, _productRepo, _logRepo);
        var cartItems = new List<CartItem> { new CartItem { Product = product, Quantity = 1 } };

        var order = await service.PlaceOrderAsync(cartItems, PaymentMethod.Cash, "T1", "أحمد", false, null, OrderType.Takeaway, "تغليف هدية");

        var stored = await _orderRepo.GetOrderByIdAsync(order.Id);
        Assert.NotNull(stored);
        Assert.Equal(OrderType.Takeaway, stored.OrderType);
        Assert.Equal("تغليف هدية", stored.Notes);
    }

    [Fact]
    public async Task OrderManagementService_CancelRestoresStock()
    {
        var product = new Product { Id = "p-flw", NameAr = "شاي", NameEn = "Tea", Price = 5, Category = "HotDrinks", Inventory = 10 };
        await _productRepo.AddProductAsync(product);
        var service = new OrderManagementService(_orderRepo, _productRepo, _logRepo);
        var cartItems = new List<CartItem>
        {
            new CartItem { Product = product, Quantity = 1 }
        };
        var order = await service.PlaceOrderAsync(cartItems, PaymentMethod.Cash, "T1", "أحمد", false, null);

        var productAfterOrder = await _productRepo.GetProductByIdAsync("p-flw");
        Assert.NotNull(productAfterOrder);
        Assert.Equal(9, productAfterOrder.Inventory);

        await service.ChangeOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        var updated = await _orderRepo.GetOrderByIdAsync(order.Id);
        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.Cancelled, updated.Status);

        var productAfterCancel = await _productRepo.GetProductByIdAsync("p-flw");
        Assert.NotNull(productAfterCancel);
        Assert.Equal(10, productAfterCancel.Inventory);
    }

    [Fact]
    public async Task ReportService_GetSummary_RealData()
    {
        var product = new Product { Id = "p-rpt", NameAr = "مشروب", NameEn = "Drink", Price = 10, Category = "HotDrinks", Inventory = 10 };
        await _productRepo.AddProductAsync(product);

        var orderService = new OrderManagementService(_orderRepo, _productRepo, _logRepo);
        var cartItems = new List<CartItem> { new CartItem { Product = product, Quantity = 2 } };
        var order = await orderService.PlaceOrderAsync(cartItems, PaymentMethod.Bankak, "T1", "أحمد", false, null);

        var reportService = new ReportService(_orderRepo, _productRepo, _settingsRepo);
        var summary = await reportService.GetSummaryAsync();

        Assert.Equal(20, summary.revenue);
        Assert.Equal(1, summary.orderCount);
    }

    [Fact]
    public async Task MenuManagementService_AddAndCheckLowStock()
    {
        var service = new MenuManagementService(_productRepo, _logRepo);
        await service.AddProductAsync(new Product
        {
            Id = "p-low",
            NameAr = "عصير",
            NameEn = "Juice",
            Price = 20,
            Inventory = 2,
            MaxInventory = 30,
            Category = "HotDrinks"
        });

        var products = await service.GetAllProductsAsync();
        Assert.Contains(products, p => p.Id == "p-low");

        await service.CheckLowStockAsync(5);
        var logs = await _logRepo.GetAllLogsAsync();
        Assert.Contains(logs, l => l.Message.Contains("عصير") && l.Level == "warning");
    }
}