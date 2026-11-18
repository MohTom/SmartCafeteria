using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class PdfReportServiceTests
{
    private static AppSettings CreateSettings() => new()
    {
        CafeteriaNameAr = "كافتيريا التميز",
        CafeteriaNameEn = "AL-TAMAYUZ CAFETERIA",
        CafeteriaAddress = "شارع النيل الخرطوم",
        CafeteriaPhone = "+249 123 456 789",
        CurrencyAr = "ج.س"
    };

    private static List<Order> CreateOrders() => new()
    {
        new Order
        {
            Id = "ord-1", Code = "1", Total = 30, Subtotal = 30,
            PaymentMethod = PaymentMethod.Cash, Status = OrderStatus.Received,
            Timestamp = DateTime.Today.AddHours(10),
            Items = { new OrderItem { Id = "oi-1", OrderId = "ord-1", ProductNameAr = "قهوة", ProductPrice = 30, ProductCategory = "HotDrinks", Quantity = 1 } }
        },
        new Order
        {
            Id = "ord-2", Code = "2", Total = 15, Subtotal = 15,
            PaymentMethod = PaymentMethod.Bankak, Status = OrderStatus.Cancelled,
            Timestamp = DateTime.Today.AddHours(11),
            Items = { new OrderItem { Id = "oi-2", OrderId = "ord-2", ProductNameAr = "شاي", ProductPrice = 15, ProductCategory = "HotDrinks", Quantity = 1 } }
        }
    };

    [Fact]
    public void ArabicFont_IsEmbedded()
    {
        var names = typeof(PdfReportService).Assembly.GetManifestResourceNames();
        Assert.Contains("SmartCafeteria.Services.Fonts.Cairo.ttf", names);
    }

    [Fact]
    public async Task GenerateSalesReport_CreatesPopulatedPdf()
    {
        var service = new PdfReportService();
        var path = await service.GenerateSalesReportAsync(CreateOrders(), CreateSettings(), DateTime.Today, DateTime.Today);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateOrdersReport_CreatesPopulatedPdf()
    {
        var service = new PdfReportService();
        var path = await service.GenerateOrdersReportAsync(CreateOrders(), CreateSettings(), null, null);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateInventoryReport_CreatesPopulatedPdf()
    {
        var service = new PdfReportService();
        var products = new List<Product>
        {
            new() { Id = "p1", NameAr = "قهوة", Category = "HotDrinks", Inventory = 3, MaxInventory = 50 }
        };
        var path = await service.GenerateInventoryReportAsync(products, CreateSettings());

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateOperationsLogReport_CreatesPopulatedPdf()
    {
        var service = new PdfReportService();
        var logs = new List<SystemLog>
        {
            new() { Id = "l1", Timestamp = DateTime.Now, Message = "تم إنشاء الطلب #1", Level = "success" },
            new() { Id = "l2", Timestamp = DateTime.Now, Message = "فشلت الطباعة", Level = "error" }
        };
        var path = await service.GenerateOperationsLogReportAsync(logs, CreateSettings(), null, null);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }
}
