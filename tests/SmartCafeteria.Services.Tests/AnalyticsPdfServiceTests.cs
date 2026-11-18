using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using SmartCafeteria.Services;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class AnalyticsPdfServiceTests
{
    private static AppSettings Settings() => new()
    {
        CafeteriaNameAr = "كافتيريا التميز",
        CafeteriaAddress = "شارع النيل الخرطوم",
        CurrencyAr = "ج.س"
    };

    private static AnalyticsBundle Bundle()
    {
        var reports = new List<AnalyticsReport>();
        foreach (AnalyticsReportKind kind in Enum.GetValues<AnalyticsReportKind>())
        {
            reports.Add(new AnalyticsReport
            {
                Kind = kind,
                Title = "تقرير " + kind,
                Columns = new[] { "الصنف", "الفئة", "الكمية", "الإيرادات" },
                Rows = new List<AnalyticsRow>
                {
                    new() { Rank = "1", Primary = "قهوة", Secondary = "مشروبات", Tertiary = "الكمية: 3", Value = "30.00 ج.س", BarRatio = 1d },
                    new() { Rank = "2", Primary = "شاي", Secondary = "مشروبات", Tertiary = "الكمية: 1", Value = "10.00 ج.س", BarRatio = 0.33d }
                }
            });
        }

        return new AnalyticsBundle
        {
            PeriodLabel = "كل الفترات",
            Summary = new AnalyticsSummary { Revenue = 40m, Orders = 2, AverageBasket = 20m, TopProductName = "قهوة" },
            Kpis = new List<AnalyticsKpi>
            {
                new() { Label = "إجمالي الإيرادات", Value = "40.00 ج.س" },
                new() { Label = "عدد الطلبات", Value = "2" }
            },
            Reports = reports
        };
    }

    [Fact]
    public void ArabicFont_IsEmbeddedInTheServicesAssembly()
    {
        var names = typeof(AnalyticsPdfService).Assembly.GetManifestResourceNames();
        Assert.Contains("SmartCafeteria.Services.Fonts.Cairo.ttf", names);
    }

    [Fact]
    public void RegisteringTheArabicFontTwice_DoesNotThrow()
    {
        using var first = typeof(AnalyticsPdfService).Assembly.GetManifestResourceStream("SmartCafeteria.Services.Fonts.Cairo.ttf");
        using var second = typeof(AnalyticsPdfService).Assembly.GetManifestResourceStream("SmartCafeteria.Services.Fonts.Cairo.ttf");

        var exception = Record.Exception(() =>
        {
            QuestPDF.Drawing.FontManager.RegisterFont(first!);
            QuestPDF.Drawing.FontManager.RegisterFont(second!);
        });

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(AnalyticsReportKind.TopItems)]
    [InlineData(AnalyticsReportKind.SalesByCategory)]
    [InlineData(AnalyticsReportKind.PaymentMethods)]
    [InlineData(AnalyticsReportKind.OrderTypes)]
    [InlineData(AnalyticsReportKind.OrdersWithNotes)]
    [InlineData(AnalyticsReportKind.CashierPerformance)]
    [InlineData(AnalyticsReportKind.SalesByHour)]
    [InlineData(AnalyticsReportKind.SalesByDay)]
    [InlineData(AnalyticsReportKind.SalesByWeekday)]
    [InlineData(AnalyticsReportKind.MenuEngineering)]
    [InlineData(AnalyticsReportKind.DeadStock)]
    [InlineData(AnalyticsReportKind.ReorderPriority)]
    [InlineData(AnalyticsReportKind.FavouriteCustomizations)]
    [InlineData(AnalyticsReportKind.ExpensesByCategory)]
    [InlineData(AnalyticsReportKind.ProfitAndLoss)]
    [InlineData(AnalyticsReportKind.Suppliers)]
    [InlineData(AnalyticsReportKind.Employees)]
    [InlineData(AnalyticsReportKind.ErrorLog)]
    public async Task GenerateAnalyticsReportAsync_CreatesAPdfForEveryReport(AnalyticsReportKind kind)
    {
        var service = new AnalyticsPdfService();

        var path = await service.GenerateAnalyticsReportAsync(Bundle(), kind, Settings());

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    private static AnalyticsBundle WithKpis(AnalyticsBundle bundle, int count) => new()
    {
        Start = bundle.Start,
        End = bundle.End,
        PeriodLabel = bundle.PeriodLabel,
        Summary = bundle.Summary,
        Kpis = Enumerable.Range(1, count)
            .Select(i => new AnalyticsKpi { Label = "مؤشر " + i, Value = (i * 10).ToString("N2") + " ج.س" })
            .ToList(),
        Reports = bundle.Reports,
        Financial = bundle.Financial
    };

    [Fact]
    public async Task GenerateAnalyticsReportAsync_IgnoresTheKpiDashboard()
    {
        var service = new AnalyticsPdfService();
        var bundle = Bundle();

        var withKpis = await service.GenerateAnalyticsReportAsync(WithKpis(bundle, 12), AnalyticsReportKind.TopItems, Settings());
        var withKpisLength = new FileInfo(withKpis).Length;
        File.Delete(withKpis);

        var withoutKpis = await service.GenerateAnalyticsReportAsync(WithKpis(bundle, 0), AnalyticsReportKind.TopItems, Settings());
        var withoutKpisLength = new FileInfo(withoutKpis).Length;
        File.Delete(withoutKpis);

        var delta = Math.Abs(withKpisLength - withoutKpisLength);
        Assert.True(delta <= 64, $"التقرير الفردي يجب ألا يتأثر بلوحة المؤشرات: delta={delta} (مع={withKpisLength} بدون={withoutKpisLength})");
    }

    [Fact]
    public async Task GenerateFullAnalyticsReportAsync_KeepsTheKpiDashboard()
    {
        var service = new AnalyticsPdfService();
        var bundle = Bundle();

        var withKpis = await service.GenerateFullAnalyticsReportAsync(WithKpis(bundle, 12), Settings());
        var withKpisLength = new FileInfo(withKpis).Length;
        File.Delete(withKpis);

        var withoutKpis = await service.GenerateFullAnalyticsReportAsync(WithKpis(bundle, 0), Settings());
        var withoutKpisLength = new FileInfo(withoutKpis).Length;
        File.Delete(withoutKpis);

        var delta = withKpisLength - withoutKpisLength;
        Assert.True(delta >= 200, $"التقرير الشامل يجب أن يحتفظ بلوحة المؤشرات: delta={delta} (مع={withKpisLength} بدون={withoutKpisLength})");
    }

    [Fact]
    public void EveryAnalyticalTableHeader_FitsOnASingleLine()
    {
        var bundle = RealReports();
        var widths = AnalyticsPdfService.AnalyticalColumnWidths();

        using var text = ArabicTextWidth.Cairo(11);

        foreach (var report in bundle.Reports)
        {
            var headers = new[] { AnalyticsPdfService.RankHeader }.Concat(report.Columns).ToArray();

            for (var index = 0; index < headers.Length && index < widths.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(headers[index])) continue;

                var textSpace = widths[index] - 2 * AnalyticsPdfService.CellPadding;
                var measured = text.Measure(headers[index]);

                Assert.True(measured * 1.10 <= textSpace,
                    $"الترويسة '{headers[index]}' في تقرير {report.Kind} تحتاج {measured:F2} نقطة والعمود {index} يعرض {textSpace:F2} نقطة");
            }
        }
    }

    private static AnalyticsBundle RealReports()
    {
        var orders = new Mock<IOrderService>();
        orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());

        var products = new Mock<IProductService>();
        products.Setup(p => p.GetAllProductsAsync()).ReturnsAsync(Array.Empty<Product>());

        var accounts = new Mock<IAccountService>();
        accounts.Setup(a => a.GetAllEntriesAsync()).ReturnsAsync(Array.Empty<AccountEntry>());
        accounts.Setup(a => a.GetEntriesByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<AccountEntry>());

        var suppliers = new Mock<ISupplierService>();
        suppliers.Setup(s => s.GetAllSuppliersAsync()).ReturnsAsync(Array.Empty<Supplier>());
        suppliers.Setup(s => s.GetAllPurchaseOrdersAsync()).ReturnsAsync(Array.Empty<PurchaseOrder>());
        suppliers.Setup(s => s.GetPaymentsForSupplierAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<SupplierPayment>());

        var employees = new Mock<IEmployeeService>();
        employees.Setup(e => e.GetAllEmployeesAsync()).ReturnsAsync(Array.Empty<Employee>());

        var logs = new Mock<ILogService>();
        logs.Setup(l => l.GetAllLogsAsync()).ReturnsAsync(Array.Empty<SystemLog>());

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new AppSettings { CurrencyAr = "ج.س" });

        var service = new AnalyticsService(
            orders.Object,
            products.Object,
            accounts.Object,
            suppliers.Object,
            employees.Object,
            logs.Object,
            settings.Object);

        return service.BuildAsync(null, null).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GenerateAnalyticsReportAsync_CreatesAValidPdfFile()
    {
        var service = new AnalyticsPdfService();

        var path = await service.GenerateAnalyticsReportAsync(Bundle(), AnalyticsReportKind.TopItems, Settings());

        string text;
        using (var stream = File.OpenRead(path))
        {
            var header = new byte[8];
            stream.ReadExactly(header);
            text = System.Text.Encoding.ASCII.GetString(header);
        }

        Assert.True(text.StartsWith("%PDF-"), $"header='{text}'");
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateAnalyticsReportAsync_WorksWhenTheReportHasNoRows()
    {
        var bundle = Bundle();
        var empty = new List<AnalyticsReport>();
        foreach (var report in bundle.Reports)
        {
            empty.Add(new AnalyticsReport { Kind = report.Kind, Title = report.Title, Columns = report.Columns, Rows = Array.Empty<AnalyticsRow>() });
        }

        var service = new AnalyticsPdfService();
        var path = await service.GenerateAnalyticsReportAsync(
            new AnalyticsBundle { PeriodLabel = bundle.PeriodLabel, Summary = bundle.Summary, Kpis = bundle.Kpis, Reports = empty },
            AnalyticsReportKind.TopItems,
            Settings());

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateFullAnalyticsReportAsync_CreatesOneDocumentWithEveryReport()
    {
        var service = new AnalyticsPdfService();

        var fullPath = await service.GenerateFullAnalyticsReportAsync(Bundle(), Settings());
        var singlePath = await service.GenerateAnalyticsReportAsync(Bundle(), AnalyticsReportKind.TopItems, Settings());

        Assert.True(File.Exists(fullPath));
        Assert.True(new FileInfo(fullPath).Length > new FileInfo(singlePath).Length);
        File.Delete(fullPath);
        File.Delete(singlePath);
    }

    [Fact]
    public async Task GenerateAnalyticsReportAsync_RejectsAReportTheBundleDoesNotCarry()
    {
        var service = new AnalyticsPdfService();
        var bundle = new AnalyticsBundle
        {
            PeriodLabel = "كل الفترات",
            Summary = new AnalyticsSummary(),
            Kpis = Array.Empty<AnalyticsKpi>(),
            Reports = Array.Empty<AnalyticsReport>()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnalyticsReportAsync(bundle, AnalyticsReportKind.TopItems, Settings()));
    }

    private static FinancialReport Financial()
    {
        return new FinancialReport
        {
            PeriodLabel = "من 2026/03/01 إلى 2026/03/31",
            Currency = "ج.س",
            Operations = new List<FinancialOperation>
            {
                new() { Date = new DateTime(2026, 3, 3), KindLabel = "أمر شراء", Description = "أمر شراء PO-1", Party = "مورد الألبان", PaymentLabel = "آجل", Direction = FinancialDirection.Commitment, Amount = 500m, Reference = "PO-1" },
                new() { Date = new DateTime(2026, 3, 5), KindLabel = "رواتب", Description = "راتب مارس", Party = "علي حسن", PaymentLabel = "نقداً", Direction = FinancialDirection.Outflow, Amount = 200m, Reference = "a2" },
                new() { Date = new DateTime(2026, 3, 7), KindLabel = "دفعة مورد", Description = "دفعة لمورد", Party = "مورد الألبان", PaymentLabel = "بنكك", Direction = FinancialDirection.Outflow, Amount = 60m, Reference = "p1" },
                new() { Date = new DateTime(2026, 3, 10), KindLabel = "مبيعات", Description = "طلب o1", Party = "سارة", PaymentLabel = "نقداً", Direction = FinancialDirection.Inflow, Amount = 120m, Reference = "o1" }
            },
            Summary = new FinancialSummary
            {
                Sales = 120m,
                OtherIncome = 30m,
                SupplierPayments = 60m,
                SalaryPayments = 200m,
                OtherExpenses = 75m,
                SupplierPurchases = 500m,
                Operations = 4
            }
        };
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_CreatesAValidPdfFile()
    {
        var service = new AnalyticsPdfService();

        var path = await service.GenerateFinancialReportAsync(Financial(), Settings());

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);

        string text;
        using (var stream = File.OpenRead(path))
        {
            var header = new byte[8];
            stream.ReadExactly(header);
            text = System.Text.Encoding.ASCII.GetString(header);
        }

        Assert.True(text.StartsWith("%PDF-"), $"header='{text}'");
        Assert.Contains("FinancialReport_", Path.GetFileName(path));
        File.Delete(path);
    }

    [Fact]
    public async Task GenerateFinancialReportAsync_StillWritesTheSummaryForAnEmptyPeriod()
    {
        var service = new AnalyticsPdfService();
        var empty = new FinancialReport
        {
            PeriodLabel = "كل الفترات",
            Currency = "ج.س",
            Operations = Array.Empty<FinancialOperation>(),
            Summary = new FinancialSummary()
        };

        var path = await service.GenerateFinancialReportAsync(empty, Settings());

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 1000);
        File.Delete(path);
    }
}
