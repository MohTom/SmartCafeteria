using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using SmartCafeteria.Services.Analytics;

namespace SmartCafeteria.Services;

/// <summary>Builds the analytical reports of the reports screen. It reads the
/// source data once per request and assembles every report from that single
/// snapshot, so the numbers of all reports always agree with each other.
/// The service never writes: the database is only read.</summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IAccountService _accountService;
    private readonly ISupplierService _supplierService;
    private readonly IEmployeeService _employeeService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public AnalyticsService(
        IOrderService orderService,
        IProductService productService,
        IAccountService accountService,
        ISupplierService supplierService,
        IEmployeeService employeeService,
        ILogService logService,
        ISettingsService settingsService)
    {
        _orderService = orderService;
        _productService = productService;
        _accountService = accountService;
        _supplierService = supplierService;
        _employeeService = employeeService;
        _logService = logService;
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    public async Task<AnalyticsBundle> BuildAsync(DateTime? startDate, DateTime? endDate)
    {
        var (start, end) = NormalizeRange(startDate, endDate);
        var currency = await ResolveCurrencyAsync();

        var allOrders = (await _orderService.GetAllOrdersAsync()).ToList();
        var orders = allOrders
            .Where(o => InRange(o.Timestamp, start, end))
            .ToList();
        var received = orders.Where(o => o.Status == OrderStatus.Received).ToList();

        var products = (await _productService.GetAllProductsAsync()).ToList();
        var entries = await LoadAccountEntriesAsync(start, end);
        var suppliers = (await _supplierService.GetAllSuppliersAsync()).ToList();
        var purchaseOrders = (await _supplierService.GetAllPurchaseOrdersAsync()).ToList();
        var payments = await LoadSupplierPaymentsAsync(suppliers);
        var employees = (await _employeeService.GetAllEmployeesAsync()).ToList();
        var logs = (await _logService.GetAllLogsAsync())
            .Where(l => InRange(l.Timestamp, start, end))
            .ToList();

        var input = new AnalyticsInput
        {
            Orders = orders,
            AllOrders = allOrders,
            ReceivedOrders = received,
            Products = products,
            AccountEntries = entries,
            Suppliers = suppliers,
            PurchaseOrders = purchaseOrders,
            SupplierPayments = payments,
            Employees = employees,
            Logs = logs,
            Currency = currency,
            RangeDays = RangeDays(start, end, orders)
        };

        var summary = BuildSummary(input);
        var reports = new List<AnalyticsReport>
        {
            Report(AnalyticsReportKind.TopItems, "الأصناف الأكثر مبيعاً", new[] { "الصنف", "الفئة", "الكمية", "الإيرادات" }, AnalyticsSalesCalculators.TopItems(input)),
            Report(AnalyticsReportKind.SalesByCategory, "المبيعات حسب الفئة", new[] { "الفئة", "عدد الطلبات", "الكمية", "الإيرادات" }, AnalyticsSalesCalculators.SalesByCategory(input)),
            Report(AnalyticsReportKind.PaymentMethods, "توزيع طرق الدفع", new[] { "طريقة الدفع", "النسبة", "عدد الطلبات", "المبلغ" }, AnalyticsSalesCalculators.PaymentMethods(input)),
            Report(AnalyticsReportKind.OrderTypes, "توزيع أنواع الطلبات", new[] { "نوع الطلب", "النسبة", "عدد الطلبات", "المبلغ" }, AnalyticsSalesCalculators.OrderTypes(input)),
            Report(AnalyticsReportKind.OrdersWithNotes, "الطلبات حسب الملاحظات", new[] { "الحالة", "النسبة", "عدد الطلبات", "المبلغ" }, AnalyticsSalesCalculators.OrdersWithNotes(input)),
            Report(AnalyticsReportKind.CashierPerformance, "أداء الكاشيرين", new[] { "الكاشير", "متوسط الطلب", "عدد الطلبات", "المبيعات" }, AnalyticsSalesCalculators.CashierPerformance(input)),
            Report(AnalyticsReportKind.SalesByHour, "المبيعات حسب الساعة", new[] { "الساعة", "القطع المباعة", "عدد الطلبات", "المبيعات" }, AnalyticsSalesCalculators.SalesByHour(input)),
            Report(AnalyticsReportKind.SalesByDay, "المبيعات حسب اليوم", new[] { "اليوم", "القطع المباعة", "عدد الطلبات", "المبيعات" }, AnalyticsSalesCalculators.SalesByDay(input)),
            Report(AnalyticsReportKind.SalesByWeekday, "المبيعات حسب يوم الأسبوع", new[] { "يوم الأسبوع", "القطع المباعة", "عدد الطلبات", "المبيعات" }, AnalyticsSalesCalculators.SalesByWeekday(input)),
            Report(AnalyticsReportKind.MenuEngineering, "هندسة القائمة", new[] { "الصنف", "الفئة", "التصنيف والإيراد للوحدة", "الكمية والإيرادات" }, AnalyticsMenuCalculators.MenuEngineering(input)),
            Report(AnalyticsReportKind.DeadStock, "الأصناف الراكدة", new[] { "الصنف", "الفئة", "آخر بيع", "المخزون" }, AnalyticsMenuCalculators.DeadStock(input)),
            Report(AnalyticsReportKind.ReorderPriority, "أولوية إعادة الطلب", new[] { "الصنف", "الفئة", "متوسط التغطية", "المقترح" }, AnalyticsMenuCalculators.ReorderPriority(input)),
            Report(AnalyticsReportKind.FavouriteCustomizations, "الإضافات المفضلة", new[] { "الإضافة", "إيراد إضافي", "عدد الاستخدام", "الحصة" }, AnalyticsMenuCalculators.FavouriteCustomizations(input)),
            Report(AnalyticsReportKind.ExpensesByCategory, "المصروفات حسب البند", new[] { "بند المصروف", "عدد القيود", "النسبة", "المبلغ" }, AnalyticsFinanceCalculators.ExpensesByCategory(input)),
            Report(AnalyticsReportKind.ProfitAndLoss, "قائمة الأرباح والخسائر", new[] { "البند", "", "النسبة من المبيعات", "المبلغ" }, AnalyticsFinanceCalculators.ProfitAndLoss(input)),
            Report(AnalyticsReportKind.Suppliers, "الموردون والأرصدة", new[] { "المورد", "الهاتف", "المشتريات والمدفوع", "الرصيد" }, AnalyticsFinanceCalculators.Suppliers(input)),
            Report(AnalyticsReportKind.Employees, "الموظفون والرواتب", new[] { "الموظف", "الوظيفة", "التعيين", "الراتب الشهري" }, AnalyticsFinanceCalculators.Employees(input)),
            Report(AnalyticsReportKind.ErrorLog, "سجل الأخطاء والتنبيهات", new[] { "الرسالة", "المستوى", "الوقت", "" }, AnalyticsFinanceCalculators.ErrorLog(input))
        };

        return new AnalyticsBundle
        {
            Start = start,
            End = end,
            PeriodLabel = FormatPeriod(startDate, endDate),
            Summary = summary,
            Kpis = BuildKpis(summary, currency),
            Reports = reports,
            Financial = AnalyticsFinanceCalculators.GeneralFinancial(new AnalyticsInput
            {
                Orders = orders,
                AllOrders = allOrders,
                ReceivedOrders = received,
                Products = products,
                AccountEntries = entries,
                Suppliers = suppliers,
                PurchaseOrders = purchaseOrders,
                SupplierPayments = payments,
                Employees = employees,
                Logs = logs,
                Currency = currency,
                RangeDays = input.RangeDays,
                Start = start,
                End = end
            })
        };
    }

    /// <summary>Normalizes an optional date range to whole days. The end date
    /// is inclusive: without this an end date of today (midnight) would
    /// silently exclude every order placed today.</summary>
    private static (DateTime? Start, DateTime? End) NormalizeRange(DateTime? startDate, DateTime? endDate)
    {
        DateTime? start = startDate?.Date;
        DateTime? end = endDate?.Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }

    private static bool InRange(DateTime timestamp, DateTime? start, DateTime? end)
        => (!start.HasValue || timestamp >= start.Value) && (!end.HasValue || timestamp <= end.Value);

    private static string FormatPeriod(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
            return $"من {startDate:yyyy/MM/dd} إلى {endDate:yyyy/MM/dd}";
        if (startDate.HasValue)
            return $"من {startDate:yyyy/MM/dd}";
        if (endDate.HasValue)
            return $"حتى {endDate:yyyy/MM/dd}";
        return "كل الفترات";
    }

    private static int RangeDays(DateTime? start, DateTime? end, List<Order> orders)
    {
        if (start.HasValue && end.HasValue)
            return Math.Max(1, (int)(end.Value.Date - start.Value.Date).TotalDays + 1);

        if (orders.Count == 0)
            return 1;

        var first = orders.Min(o => o.Timestamp.Date);
        var last = orders.Max(o => o.Timestamp.Date);
        return Math.Max(1, (int)(last - first).TotalDays + 1);
    }

    private async Task<string> ResolveCurrencyAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            return string.IsNullOrWhiteSpace(settings.CurrencyAr) ? "SDG" : settings.CurrencyAr;
        }
        catch (Exception)
        {
            return "SDG";
        }
    }

    private async Task<List<AccountEntry>> LoadAccountEntriesAsync(DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue)
            return (await _accountService.GetAllEntriesAsync()).ToList();

        var from = start ?? DateTime.MinValue;
        var to = end ?? DateTime.MaxValue;
        return (await _accountService.GetEntriesByDateRangeAsync(from, to)).ToList();
    }

    private async Task<List<SupplierPayment>> LoadSupplierPaymentsAsync(List<Supplier> suppliers)
    {
        var payments = new List<SupplierPayment>();
        foreach (var supplier in suppliers)
        {
            try
            {
                payments.AddRange(await _supplierService.GetPaymentsForSupplierAsync(supplier.Id));
            }
            catch (Exception)
            {
                // A supplier without payments must not break the whole report.
            }
        }

        return payments;
    }

    private static AnalyticsSummary BuildSummary(AnalyticsInput input)
    {
        var revenue = input.ReceivedOrders.Sum(o => o.Total);
        var orders = input.ReceivedOrders.Count;
        var units = input.ReceivedOrders.Sum(o => (o.Items ?? new List<OrderItem>()).Sum(i => i.Quantity));
        var expenses = input.AccountEntries.Where(e => e.Type == AccountEntryType.Expense).Sum(e => e.Amount);
        var otherIncome = input.AccountEntries.Where(e => e.Type == AccountEntryType.Income).Sum(e => e.Amount);
        var net = revenue + otherIncome - expenses;
        var cancelled = input.Orders.Where(o => o.Status == OrderStatus.Cancelled).ToList();

        var topItems = AnalyticsSalesCalculators.TopItems(input);
        var topCategories = AnalyticsSalesCalculators.SalesByCategory(input);
        var hours = AnalyticsSalesCalculators.SalesByHour(input);

        return new AnalyticsSummary
        {
            Revenue = revenue,
            Orders = orders,
            AverageBasket = orders == 0 ? 0m : revenue / orders,
            UnitsSold = units,
            ItemsPerOrder = orders == 0 ? 0m : (decimal)units / orders,
            Expenses = expenses,
            OtherIncome = otherIncome,
            NetProfit = net,
            MarginPercent = revenue <= 0m ? 0m : net / revenue * 100m,
            CancelledOrders = cancelled.Count,
            CancelledValue = cancelled.Sum(o => o.Total),
            ActiveProducts = input.Products.Count(p => p.Inventory > 0),
            DeadStockCount = input.Products.Count(p => p.Inventory > 0 && !input.ReceivedOrders
                .SelectMany(o => o.Items ?? Enumerable.Empty<OrderItem>())
                .Any(i => AnalyticsSalesCalculators.ProductKey(i) == p.Id || AnalyticsSalesCalculators.ProductKey(i) == p.NameAr)),
            TopProductName = topItems.Count == 0 ? string.Empty : topItems[0].Primary,
            TopCategoryName = topCategories.Count == 0 ? string.Empty : topCategories[0].Primary,
            BusiestHourLabel = hours.Count == 0 ? string.Empty : hours.OrderByDescending(h => h.BarRatio).First().Primary
        };
    }

    private static IReadOnlyList<AnalyticsKpi> BuildKpis(AnalyticsSummary summary, string currency)
    {
        return new List<AnalyticsKpi>
        {
            new() { Label = "💰 إجمالي الإيرادات", Value = AnalyticsFormat.Money(summary.Revenue, currency) },
            new() { Label = "🧾 عدد الطلبات", Value = AnalyticsFormat.Number(summary.Orders) },
            new() { Label = "🛒 متوسط الطلب", Value = AnalyticsFormat.Money(summary.AverageBasket, currency) },
            new() { Label = "📦 القطع المباعة", Value = AnalyticsFormat.Number(summary.UnitsSold) },
            new() { Label = "💵 المصروفات", Value = AnalyticsFormat.Money(summary.Expenses, currency) },
            new() { Label = "📈 صافي الربح", Value = AnalyticsFormat.Money(summary.NetProfit, currency) },
            new() { Label = "📊 هامش الربح", Value = $"{summary.MarginPercent.ToString("N1", System.Globalization.CultureInfo.InvariantCulture)}%" },
            new() { Label = "❌ طلبات ملغاة", Value = $"{AnalyticsFormat.Number(summary.CancelledOrders)} ({AnalyticsFormat.Money(summary.CancelledValue, currency)})" },
            new() { Label = "🏆 الصنف الأكثر مبيعاً", Value = Fallback(summary.TopProductName) },
            new() { Label = "🗂️ أفضل فئة", Value = Fallback(summary.TopCategoryName) },
            new() { Label = "⏰ ذروة الطلبات", Value = Fallback(summary.BusiestHourLabel) },
            new() { Label = "🧊 أصناف راكدة", Value = AnalyticsFormat.Number(summary.DeadStockCount) }
        };
    }

    private static AnalyticsReport Report(AnalyticsReportKind kind, string title, string[] columns, IReadOnlyList<AnalyticsRow> rows)
        => new() { Kind = kind, Title = title, Columns = columns, Rows = rows };

    /// <summary>Replaces an empty headline value with a readable placeholder so
    /// a card of the summary never shows up blank.</summary>
    private static string Fallback(string value)
        => string.IsNullOrWhiteSpace(value) ? "لا يوجد" : value;
}
