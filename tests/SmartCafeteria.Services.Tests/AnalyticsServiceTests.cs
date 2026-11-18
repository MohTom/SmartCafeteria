using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class AnalyticsServiceTests
{
    private readonly Mock<IOrderService> _orders = new();
    private readonly Mock<IProductService> _products = new();
    private readonly Mock<IAccountService> _accounts = new();
    private readonly Mock<ISupplierService> _suppliers = new();
    private readonly Mock<IEmployeeService> _employees = new();
    private readonly Mock<ILogService> _logs = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly AnalyticsService _service;

    public AnalyticsServiceTests()
    {
        _settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new AppSettings { CurrencyAr = "SDG" });
        _accounts.Setup(a => a.GetAllEntriesAsync()).ReturnsAsync(Array.Empty<AccountEntry>());
        _accounts.Setup(a => a.GetEntriesByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<AccountEntry>());
        _suppliers.Setup(s => s.GetAllSuppliersAsync()).ReturnsAsync(Array.Empty<Supplier>());
        _suppliers.Setup(s => s.GetAllPurchaseOrdersAsync()).ReturnsAsync(Array.Empty<PurchaseOrder>());
        _suppliers.Setup(s => s.GetPaymentsForSupplierAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<SupplierPayment>());
        _employees.Setup(e => e.GetAllEmployeesAsync()).ReturnsAsync(Array.Empty<Employee>());
        _logs.Setup(l => l.GetAllLogsAsync()).ReturnsAsync(Array.Empty<SystemLog>());
        _products.Setup(p => p.GetAllProductsAsync()).ReturnsAsync(Array.Empty<Product>());

        _service = new AnalyticsService(
            _orders.Object,
            _products.Object,
            _accounts.Object,
            _suppliers.Object,
            _employees.Object,
            _logs.Object,
            _settings.Object);
    }

    private static Order Order(string id, DateTime at, decimal total, OrderStatus status = OrderStatus.Received, params OrderItem[] items) => new()
    {
        Id = id,
        Code = id,
        Status = status,
        Timestamp = at,
        Total = total,
        Subtotal = total,
        PaymentMethod = PaymentMethod.Cash,
        CashierName = "سارة",
        Items = items.ToList()
    };

    private static OrderItem Item(string id, string category, decimal price, int quantity) => new()
    {
        ProductId = id,
        ProductNameAr = id,
        ProductCategory = category,
        ProductPrice = price,
        Quantity = quantity
    };

    [Fact]
    public async Task BuildAsync_ReturnsEveryReport()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());

        var bundle = await _service.BuildAsync(null, null);

        Assert.Equal(18, bundle.Reports.Count);
        Assert.Equal(18, bundle.Reports.Select(r => r.Kind).Distinct().Count());
        Assert.NotNull(bundle.Find(Core.Enums.AnalyticsReportKind.OrderTypes));
        Assert.NotNull(bundle.Find(Core.Enums.AnalyticsReportKind.OrdersWithNotes));
        Assert.NotNull(bundle.Find(Core.Enums.AnalyticsReportKind.TopItems));
        Assert.NotEmpty(bundle.Kpis);
    }

    [Fact]
    public async Task BuildAsync_CountsTheEndDateAsPartOfTheRange()
    {
        var end = new DateTime(2026, 3, 10);
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(new[]
        {
            Order("late", end.AddHours(23).AddMinutes(30), 100m),
            Order("next", end.AddDays(1), 100m)
        });

        var bundle = await _service.BuildAsync(end.AddDays(-1), end);

        Assert.Equal(1, bundle.Summary.Orders);
    }

    [Fact]
    public async Task BuildAsync_LeavesOutTheOrdersBeforeTheStartDate()
    {
        var start = new DateTime(2026, 3, 10);
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(new[]
        {
            Order("early", start.AddDays(-1), 100m),
            Order("inside", start.AddHours(2), 100m)
        });

        var bundle = await _service.BuildAsync(start, start.AddDays(1));

        Assert.Equal(1, bundle.Summary.Orders);
    }

    [Fact]
    public async Task BuildAsync_KeepsTheCancelledOrdersOutOfTheRevenueAndCountsThem()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(new[]
        {
            Order("ok", new DateTime(2026, 3, 10, 10, 0, 0), 300m),
            Order("bad", new DateTime(2026, 3, 10, 11, 0, 0), 700m, OrderStatus.Cancelled)
        });

        var bundle = await _service.BuildAsync(null, null);

        Assert.Equal(300m, bundle.Summary.Revenue);
        Assert.Equal(1, bundle.Summary.Orders);
        Assert.Equal(1, bundle.Summary.CancelledOrders);
        Assert.Equal(700m, bundle.Summary.CancelledValue);
    }

    [Fact]
    public async Task BuildAsync_ComputesTheSummaryOfTheRange()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(new[]
        {
            Order("o1", new DateTime(2026, 3, 10, 10, 0, 0), 40m, OrderStatus.Received, Item("كافيه", "مشروبات", 10m, 4)),
            Order("o2", new DateTime(2026, 3, 10, 14, 0, 0), 360m, OrderStatus.Received, Item("شاي", "مشروبات", 20m, 18))
        });
        _products.Setup(p => p.GetAllProductsAsync()).ReturnsAsync(new[]
        {
            new Product { Id = "كافيه", NameAr = "كافيه", Inventory = 4 },
            new Product { Id = "راكد", NameAr = "راكد", Inventory = 2 }
        });
        _accounts.Setup(a => a.GetAllEntriesAsync()).ReturnsAsync(new[]
        {
            new AccountEntry { Type = AccountEntryType.Expense, Category = ExpenseCategory.Rent, Amount = 100m },
            new AccountEntry { Type = AccountEntryType.Income, Category = ExpenseCategory.Other, Amount = 50m }
        });

        var bundle = await _service.BuildAsync(null, null);

        Assert.Equal(400m, bundle.Summary.Revenue);
        Assert.Equal(2, bundle.Summary.Orders);
        Assert.Equal(200m, bundle.Summary.AverageBasket);
        Assert.Equal(22, bundle.Summary.UnitsSold);
        Assert.Equal(100m, bundle.Summary.Expenses);
        Assert.Equal(50m, bundle.Summary.OtherIncome);
        Assert.Equal(350m, bundle.Summary.NetProfit);
        Assert.Equal(87.5m, bundle.Summary.MarginPercent);
        Assert.Equal("شاي", bundle.Summary.TopProductName);
        Assert.Equal("مشروبات", bundle.Summary.TopCategoryName);
        Assert.Equal("14:00 - 15:00", bundle.Summary.BusiestHourLabel);
        Assert.Equal(1, bundle.Summary.DeadStockCount);
    }

    [Fact]
    public async Task BuildAsync_ReturnsAnEmptyBundleWhenThereIsNothingToReport()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());

        var bundle = await _service.BuildAsync(null, null);

        Assert.Equal(0m, bundle.Summary.Revenue);
        Assert.Equal(0, bundle.Summary.Orders);
        Assert.Equal(0m, bundle.Summary.AverageBasket);
        Assert.All(bundle.Kpis, k => Assert.False(string.IsNullOrWhiteSpace(k.Value)));
    }

    [Fact]
    public async Task BuildAsync_LeavesTheDataReportsEmptyButKeepsTheStructuralOnes()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());

        var bundle = await _service.BuildAsync(null, null);

        // The weekday profile always shows the seven days and the profit and
        // loss statement always shows its lines, so both stay meaningful when
        // the period is empty. Every other report has no row at all.
        var structural = new[] { Core.Enums.AnalyticsReportKind.SalesByWeekday, Core.Enums.AnalyticsReportKind.ProfitAndLoss };
        foreach (var report in bundle.Reports)
        {
            if (structural.Contains(report.Kind))
                continue;

            Assert.Empty(report.Rows);
        }

        Assert.Equal(7, bundle.Find(Core.Enums.AnalyticsReportKind.SalesByWeekday)!.Rows.Count);
        Assert.Equal(5, bundle.Find(Core.Enums.AnalyticsReportKind.ProfitAndLoss)!.Rows.Count);
    }

    [Fact]
    public async Task BuildAsync_DescribesTheRangeInArabic()
    {
        _orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());

        var bundle = await _service.BuildAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        Assert.Contains("2026/03/01", bundle.PeriodLabel);
        Assert.Contains("2026/03/31", bundle.PeriodLabel);
    }
}
