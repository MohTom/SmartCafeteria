using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using SmartCafeteria.Services.Analytics;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>Guards the general financial report: one chronological ledger of every
/// money movement, and a summary that never counts the same pound twice.</summary>
public class FinancialReportTests
{
    private static readonly DateTime _start = new(2026, 3, 1);
    private static readonly DateTime _end = new(2026, 3, 31, 23, 59, 59, 999);

    private static Order Order(string id, decimal total, DateTime at, OrderStatus status = OrderStatus.Received) => new()
    {
        Id = id,
        Code = id,
        Status = status,
        Timestamp = at,
        Total = total,
        Subtotal = total,
        PaymentMethod = PaymentMethod.Cash,
        CashierName = "سارة",
        Items = new List<OrderItem>()
    };

    private static AccountEntry Entry(
        string id,
        AccountEntryType type,
        ExpenseCategory category,
        decimal amount,
        DateTime at,
        string description = "",
        string reference = "") => new()
    {
        Id = id,
        Type = type,
        Category = category,
        Description = description,
        Amount = amount,
        Date = at,
        PaymentMethod = PaymentMethod.Cash,
        ReferenceCode = reference,
        UserName = "المدير"
    };

    private static SupplierPayment Payment(string id, string supplierId, decimal amount, DateTime at) => new()
    {
        Id = id,
        SupplierId = supplierId,
        Amount = amount,
        Date = at,
        PaymentMethod = PaymentMethod.Bankak,
        Notes = ""
    };

    private static PurchaseOrder PurchaseOrder(
        string id,
        string supplierId,
        decimal amount,
        DateTime at,
        PurchaseOrderStatus status = PurchaseOrderStatus.Received) => new()
    {
        Id = id,
        Code = "PO-" + id,
        SupplierId = supplierId,
        SupplierName = "مورد " + supplierId,
        Date = at,
        TotalAmount = amount,
        Status = status,
        Items = new List<PurchaseOrderItem>()
    };

    private static AnalyticsInput Input(
        IEnumerable<Order>? orders = null,
        IEnumerable<AccountEntry>? entries = null,
        IEnumerable<Supplier>? suppliers = null,
        IEnumerable<PurchaseOrder>? purchaseOrders = null,
        IEnumerable<SupplierPayment>? payments = null,
        IEnumerable<Employee>? employees = null,
        DateTime? start = null,
        DateTime? end = null)
    {
        var all = (orders ?? Array.Empty<Order>()).ToList();
        return new AnalyticsInput
        {
            Orders = all,
            AllOrders = all,
            ReceivedOrders = all.Where(o => o.Status == OrderStatus.Received).ToList(),
            Products = Array.Empty<Product>(),
            AccountEntries = (entries ?? Array.Empty<AccountEntry>()).ToList(),
            Suppliers = (suppliers ?? Array.Empty<Supplier>()).ToList(),
            PurchaseOrders = (purchaseOrders ?? Array.Empty<PurchaseOrder>()).ToList(),
            SupplierPayments = (payments ?? Array.Empty<SupplierPayment>()).ToList(),
            Employees = (employees ?? Array.Empty<Employee>()).ToList(),
            Logs = Array.Empty<SystemLog>(),
            Currency = "SDG",
            RangeDays = 31,
            Start = start,
            End = end
        };
    }

    private static AnalyticsInput FullPeriod()
    {
        var suppliers = new[]
        {
            new Supplier { Id = "s1", Name = "مورد الألبان", Phone = "111" }
        };
        var employees = new[]
        {
            new Employee { Id = "e1", Name = "علي حسن", Position = EmployeePosition.Barista, MonthlySalary = 200m }
        };

        return Input(
            orders: new[]
            {
                Order("o1", 120m, new DateTime(2026, 3, 10, 10, 0, 0)),
                Order("o2", 50m, new DateTime(2026, 3, 12, 10, 0, 0), OrderStatus.Cancelled)
            },
            entries: new[]
            {
                Entry("a1", AccountEntryType.Income, ExpenseCategory.Other, 30m, new DateTime(2026, 3, 11), "بيع معدات قديمة"),
                Entry("a2", AccountEntryType.Expense, ExpenseCategory.Salaries, 200m, new DateTime(2026, 3, 5), "راتب مارس", "e1"),
                Entry("a3", AccountEntryType.Expense, ExpenseCategory.Rent, 75m, new DateTime(2026, 3, 6), "إيجار")
            },
            suppliers: suppliers,
            purchaseOrders: new[]
            {
                PurchaseOrder("po1", "s1", 500m, new DateTime(2026, 3, 3)),
                PurchaseOrder("po2", "s1", 300m, new DateTime(2026, 3, 4), PurchaseOrderStatus.Cancelled),
                PurchaseOrder("po3", "s1", 400m, new DateTime(2025, 1, 1))
            },
            payments: new[]
            {
                Payment("p1", "s1", 60m, new DateTime(2026, 3, 7)),
                Payment("p2", "s1", 999m, new DateTime(2025, 1, 1))
            },
            employees: employees,
            start: _start,
            end: _end);
    }

    [Fact]
    public void IncludesReceivedSalesAndExcludesCancelledOnes()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        var sales = report.Operations.Where(o => o.KindLabel == "مبيعات").ToList();
        Assert.Single(sales);
        Assert.Equal(120m, sales[0].Amount);
        Assert.Equal(FinancialDirection.Inflow, sales[0].Direction);
        Assert.DoesNotContain(report.Operations, o => o.Reference == "o2");
    }

    [Fact]
    public void SeparatesSalaryPaymentsFromOtherExpenses()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.Equal(200m, report.Summary.SalaryPayments);
        Assert.Equal(75m, report.Summary.OtherExpenses);
        Assert.Equal(275m, report.Summary.TotalOutflow - report.Summary.SupplierPayments);

        var salary = report.Operations.Single(o => o.KindLabel == "رواتب");
        Assert.Equal(FinancialDirection.Outflow, salary.Direction);
        Assert.Equal("علي حسن", salary.Party);
    }

    [Fact]
    public void CountsOnlyTheSupplierPaymentsInsideTheRange()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.Equal(60m, report.Summary.SupplierPayments);
        Assert.DoesNotContain(report.Operations, o => o.Reference == "p2");
    }

    [Fact]
    public void ShowsPurchaseOrdersAsCommitmentsNotAsCash()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.Equal(500m, report.Summary.SupplierPurchases);
        Assert.DoesNotContain(500m, new[] { report.Summary.TotalOutflow });

        var commitment = report.Operations.Single(o => o.Direction == FinancialDirection.Commitment);
        Assert.Equal(500m, commitment.Amount);
    }

    [Fact]
    public void ExcludesCancelledPurchaseOrders()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.DoesNotContain(report.Operations, o => o.Reference == "PO-po2");
        Assert.DoesNotContain(300m, new[] { report.Summary.SupplierPurchases });
    }

    [Fact]
    public void ResolvesSupplierAndEmployeeNames()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.Equal("مورد الألبان", report.Operations.Single(o => o.KindLabel == "دفعة مورد").Party);
        Assert.Equal("علي حسن", report.Operations.Single(o => o.KindLabel == "رواتب").Party);
        Assert.Equal("سارة", report.Operations.Single(o => o.KindLabel == "مبيعات").Party);
    }

    [Fact]
    public void FallsBackToAPlaceholderWhenThePartyWasDeleted()
    {
        var input = Input(
            entries: new[]
            {
                Entry("a1", AccountEntryType.Expense, ExpenseCategory.Salaries, 100m, new DateTime(2026, 3, 5), "راتب", "gone")
            },
            payments: new[] { Payment("p1", "gone", 25m, new DateTime(2026, 3, 6)) },
            start: _start,
            end: _end);

        var report = AnalyticsFinanceCalculators.GeneralFinancial(input);

        Assert.Equal("موظف", report.Operations.Single(o => o.KindLabel == "رواتب").Party);
        Assert.Equal("مورد", report.Operations.Single(o => o.KindLabel == "دفعة مورد").Party);
    }

    [Fact]
    public void BuildsASummaryThatNeverDoubleCounts()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());
        var summary = report.Summary;

        Assert.Equal(120m, summary.Sales);
        Assert.Equal(30m, summary.OtherIncome);
        Assert.Equal(150m, summary.TotalInflow);
        Assert.Equal(60m, summary.SupplierPayments);
        Assert.Equal(200m, summary.SalaryPayments);
        Assert.Equal(75m, summary.OtherExpenses);
        Assert.Equal(335m, summary.TotalOutflow);
        Assert.Equal(-185m, summary.NetCashFlow);
        Assert.Equal(500m, summary.SupplierPurchases);
        Assert.Equal(440m, summary.SupplierMovement);
        Assert.Equal(6, summary.Operations);
    }

    [Fact]
    public void SortsEveryOperationChronologically()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        var dates = report.Operations.Select(o => o.Date).ToList();
        Assert.Equal(dates.OrderBy(d => d), dates);
    }

    [Fact]
    public void TreatsTheRangeEndAsInclusiveOfTheWholeDay()
    {
        var input = Input(
            orders: new[] { Order("late", 40m, new DateTime(2026, 3, 31, 23, 30, 0)) },
            start: _start,
            end: _end);

        var report = AnalyticsFinanceCalculators.GeneralFinancial(input);

        Assert.Equal(40m, report.Summary.Sales);
    }

    [Fact]
    public void ReturnsZeroTotalsForAnEmptyPeriod()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(
            Input(start: _start, end: _end));

        Assert.Empty(report.Operations);
        Assert.Equal(0m, report.Summary.TotalInflow);
        Assert.Equal(0m, report.Summary.TotalOutflow);
        Assert.Equal(0m, report.Summary.NetCashFlow);
        Assert.Equal(0, report.Summary.Operations);
        Assert.Equal("SDG", report.Currency);
    }

    [Fact]
    public void KeepsThePeriodLabelOfTheRange()
    {
        var report = AnalyticsFinanceCalculators.GeneralFinancial(FullPeriod());

        Assert.Contains("2026/03/01", report.PeriodLabel);
        Assert.Contains("2026/03/31", report.PeriodLabel);
    }
}
