using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services.Analytics;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class AnalyticsCalculatorsTests
{
    private static readonly DateTime _now = new(2026, 3, 10, 14, 30, 0);

    private static Order Order(string id, decimal total, DateTime at, params OrderItem[] items) => new()
    {
        Id = id,
        Code = id,
        Status = OrderStatus.Received,
        Timestamp = at,
        Total = total,
        Subtotal = total,
        PaymentMethod = PaymentMethod.Cash,
        CashierName = "سارة",
        Items = items.ToList()
    };

    private static OrderItem Item(string id, string category, decimal price, int quantity, decimal customizations = 0m, string customizationsJson = "[]") => new()
    {
        ProductId = id,
        ProductNameAr = id,
        ProductCategory = category,
        ProductPrice = price,
        Quantity = quantity,
        CustomizationsTotal = customizations,
        SelectedCustomizationsJson = customizationsJson
    };

    private static AnalyticsInput Input(
        IEnumerable<Order>? orders = null,
        IEnumerable<Product>? products = null,
        IEnumerable<AccountEntry>? entries = null,
        IEnumerable<Supplier>? suppliers = null,
        IEnumerable<PurchaseOrder>? purchaseOrders = null,
        IEnumerable<SupplierPayment>? payments = null,
        IEnumerable<Employee>? employees = null,
        IEnumerable<SystemLog>? logs = null,
        int rangeDays = 30)
    {
        var all = (orders ?? Array.Empty<Order>()).ToList();
        return new AnalyticsInput
        {
            Orders = all,
            AllOrders = all,
            ReceivedOrders = all.Where(o => o.Status == OrderStatus.Received).ToList(),
            Products = (products ?? Array.Empty<Product>()).ToList(),
            AccountEntries = (entries ?? Array.Empty<AccountEntry>()).ToList(),
            Suppliers = (suppliers ?? Array.Empty<Supplier>()).ToList(),
            PurchaseOrders = (purchaseOrders ?? Array.Empty<PurchaseOrder>()).ToList(),
            SupplierPayments = (payments ?? Array.Empty<SupplierPayment>()).ToList(),
            Employees = (employees ?? Array.Empty<Employee>()).ToList(),
            Logs = (logs ?? Array.Empty<SystemLog>()).ToList(),
            Currency = "SDG",
            RangeDays = rangeDays
        };
    }

    [Fact]
    public void TopItems_PutsTheBestSellerFirst()
    {
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 5)),
            Order("o2", 300m, _now, Item("شاي", "مشروبات", 10m, 10))
        });

        var rows = AnalyticsSalesCalculators.TopItems(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("شاي", rows[0].Primary);
        Assert.Equal("كافيه", rows[1].Primary);
        Assert.Equal("1", rows[0].Rank);
    }

    [Fact]
    public void TopItems_AddsTheCustomizationExtraToTheRevenueOfTheLine()
    {
        var input = Input(new[]
        {
            Order("o1", 80m, _now, Item("كافيه", "مشروبات", 10m, 2, 60m))
        });

        var rows = AnalyticsSalesCalculators.TopItems(input);

        Assert.Contains("80", rows[0].Value);
        Assert.Contains("2", rows[0].Tertiary);
    }

    [Fact]
    public void TopItems_LeavesOutCancelledOrders()
    {
        var cancelled = Order("o2", 500m, _now, Item("شاي", "مشروبات", 10m, 10));
        cancelled.Status = OrderStatus.Cancelled;
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 5)), cancelled });

        var rows = AnalyticsSalesCalculators.TopItems(input);

        Assert.Single(rows);
        Assert.Equal("كافيه", rows[0].Primary);
    }

    [Fact]
    public void SalesByCategory_GroupsTheRevenueOfEveryCategory()
    {
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 5), Item("كعك", "حلويات", 20m, 2))
        });

        var rows = AnalyticsSalesCalculators.SalesByCategory(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("مشروبات", rows[0].Primary);
        Assert.Equal("حلويات", rows[1].Primary);
        Assert.Contains("50", rows[0].Value);
        Assert.Contains("40", rows[1].Value);
    }

    [Fact]
    public void PaymentMethods_SplitsTheOrdersByHowTheyWerePaid()
    {
        var bank = Order("o2", 200m, _now, Item("شاي", "مشروبات", 10m, 4));
        bank.PaymentMethod = PaymentMethod.Bankak;
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)), bank });

        var rows = AnalyticsSalesCalculators.PaymentMethods(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("بنكك", rows[0].Primary);
        Assert.Contains("200", rows[0].Value);
        Assert.Equal("نقداً", rows[1].Primary);
    }

    [Fact]
    public void OrderTypes_SplitsTheOrdersByLocalAndTakeaway()
    {
        var takeaway = Order("o2", 200m, _now, Item("شاي", "مشروبات", 10m, 4));
        takeaway.OrderType = OrderType.Takeaway;
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)), takeaway });

        var rows = AnalyticsSalesCalculators.OrderTypes(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("سفري", rows[0].Primary);
        Assert.Contains("200", rows[0].Value);
        Assert.Equal("محلي", rows[1].Primary);
        Assert.Contains("100", rows[1].Value);
    }

    [Fact]
    public void OrderTypes_DefaultsEveryUntypedOrderToLocal()
    {
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)) });

        var rows = AnalyticsSalesCalculators.OrderTypes(input);

        var row = Assert.Single(rows);
        Assert.Equal("محلي", row.Primary);
        Assert.Contains("100", row.Value);
    }

    [Fact]
    public void OrdersWithNotes_SeparatesTheNotedOrdersFromTheRest()
    {
        var noted = Order("o2", 200m, _now, Item("شاي", "مشروبات", 10m, 4));
        noted.Notes = "بدون سكر";
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)), noted });

        var rows = AnalyticsSalesCalculators.OrdersWithNotes(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("بها ملاحظات", rows[0].Primary);
        Assert.Contains("200", rows[0].Value);
        Assert.Equal("بدون ملاحظات", rows[1].Primary);
    }

    [Fact]
    public void OrdersWithNotes_TreatsAWhitespaceNoteAsNoNote()
    {
        var blank = Order("o2", 200m, _now, Item("شاي", "مشروبات", 10m, 4));
        blank.Notes = "   ";
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)), blank });

        var rows = AnalyticsSalesCalculators.OrdersWithNotes(input);

        var row = Assert.Single(rows);
        Assert.Equal("بدون ملاحظات", row.Primary);
    }

    [Fact]
    public void CashierPerformance_GroupsTheOrdersByCashier()
    {
        var second = Order("o2", 200m, _now, Item("شاي", "مشروبات", 10m, 4));
        second.CashierName = "أحمد";
        var input = Input(new[] { Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)), second });

        var rows = AnalyticsSalesCalculators.CashierPerformance(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("أحمد", rows[0].Primary);
        Assert.Contains("200", rows[0].Value);
        Assert.Equal("سارة", rows[1].Primary);
    }

    [Fact]
    public void SalesByHour_PutsEveryOrderInItsOwnHour()
    {
        var morning = Order("o2", 100m, _now.AddHours(-3), Item("شاي", "مشروبات", 10m, 2));
        var input = Input(new[] { Order("o1", 50m, _now, Item("كافيه", "مشروبات", 10m, 1)), morning });

        var rows = AnalyticsSalesCalculators.SalesByHour(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("11:00 - 12:00", rows[0].Primary);
        Assert.Equal("14:00 - 15:00", rows[1].Primary);
    }

    [Fact]
    public void SalesByDay_GroupsTheOrdersOfTheSameDay()
    {
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 2)),
            Order("o2", 50m, _now.AddMinutes(30), Item("شاي", "مشروبات", 10m, 1)),
            Order("o3", 70m, _now.AddDays(1), Item("عصير", "مشروبات", 10m, 3))
        });

        var rows = AnalyticsSalesCalculators.SalesByDay(input);

        Assert.Equal(2, rows.Count);
        Assert.Equal("2026/03/10", rows[0].Primary);
        Assert.Contains("150", rows[0].Value);
        Assert.Equal("2026/03/11", rows[1].Primary);
    }

    [Fact]
    public void SalesByWeekday_ReturnsTheSevenDaysInArabic()
    {
        var rows = AnalyticsSalesCalculators.SalesByWeekday(Input());

        Assert.Equal(7, rows.Count);
        Assert.Equal("السبت", rows[0].Primary);
        Assert.Equal("الأحد", rows[1].Primary);
        Assert.Equal("الجمعة", rows[6].Primary);
    }

    [Fact]
    public void MenuEngineering_ClassifiesEachProductByPopularityAndProfit()
    {
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("حصان", "مشروبات", 10m, 10)),
            Order("o2", 300m, _now, Item("نجمة", "مشروبات", 30m, 10)),
            Order("o3", 20m, _now, Item("لغز", "مشروبات", 20m, 1)),
            Order("o4", 4m, _now, Item("راكد", "مشروبات", 4m, 1))
        });

        var rows = AnalyticsMenuCalculators.MenuEngineering(input);

        Assert.Equal("حصان", Classification(rows, "حصان"));
        Assert.Equal("نجمة", Classification(rows, "نجمة"));
        Assert.Equal("لغز", Classification(rows, "لغز"));
        Assert.Equal("راكد", Classification(rows, "راكد"));
    }

    [Fact]
    public void DeadStock_ListsAProductThatHasStockButNoSale()
    {
        var input = Input(
            new[] { Order("o1", 100m, _now, Item("مباع", "مشروبات", 10m, 2)) },
            new[]
            {
                new Product { Id = "مباع", NameAr = "مباع", Inventory = 5 },
                new Product { Id = "راكد", NameAr = "راكد", Inventory = 3 }
            });

        var rows = AnalyticsMenuCalculators.DeadStock(input);

        Assert.Single(rows);
        Assert.Equal("راكد", rows[0].Primary);
        Assert.Contains("لم يُبَع قط", rows[0].Tertiary);
    }

    [Fact]
    public void DeadStock_ShowsWhenTheProductWasSoldLast()
    {
        var input = Input(
            new[] { Order("o1", 100m, _now, Item("مباع", "مشروبات", 10m, 2)) },
            new[] { new Product { Id = "مباع", NameAr = "مباع", Inventory = 5 } },
            logs: Array.Empty<SystemLog>());

        var rows = AnalyticsMenuCalculators.DeadStock(input);

        Assert.Empty(rows);
    }

    [Fact]
    public void ReorderPriority_SuggestsAnOrderWhenTheStockRunsOutWithinThreeDays()
    {
        var input = Input(
            new[] { Order("o1", 3000m, _now, Item("منخفض", "مشروبات", 10m, 300)) },
            new[] { new Product { Id = "منخفض", NameAr = "منخفض", Inventory = 5, MaxInventory = 50 } },
            rangeDays: 30);

        var rows = AnalyticsMenuCalculators.ReorderPriority(input);

        Assert.Single(rows);
        Assert.Contains("45", rows[0].Value);
    }

    [Fact]
    public void ReorderPriority_LeavesOutAProductThatHasEnoughCover()
    {
        var input = Input(
            new[] { Order("o1", 300m, _now, Item("كافي", "مشروبات", 10m, 30)) },
            new[] { new Product { Id = "كافي", NameAr = "كافي", Inventory = 50, MaxInventory = 60 } },
            rangeDays: 30);

        var rows = AnalyticsMenuCalculators.ReorderPriority(input);

        Assert.Empty(rows);
    }

    [Fact]
    public void FavouriteCustomizations_CountsHowOftenEachExtraWasUsed()
    {
        var json = "[{\"Id\":\"c1\",\"NameAr\":\"حليب\",\"NameEn\":\"Milk\",\"Price\":1.5}]";
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 1, 3m, json)),
            Order("o2", 100m, _now, Item("شاي", "مشروبات", 10m, 1, 3m, json))
        });

        var rows = AnalyticsMenuCalculators.FavouriteCustomizations(input);

        Assert.Single(rows);
        Assert.Equal("حليب", rows[0].Primary);
        Assert.Contains("الاستخدام: 2", rows[0].Tertiary);
        Assert.Contains("3", rows[0].Secondary);
    }

    [Fact]
    public void FavouriteCustomizations_IgnoresBrokenJson()
    {
        var input = Input(new[]
        {
            Order("o1", 100m, _now, Item("كافيه", "مشروبات", 10m, 1, 3m, "not json"))
        });

        var rows = AnalyticsMenuCalculators.FavouriteCustomizations(input);

        Assert.Empty(rows);
    }

    [Fact]
    public void ExpensesByCategory_SumsOnlyTheExpenses()
    {
        var input = Input(entries: new[]
        {
            new AccountEntry { Type = AccountEntryType.Expense, Category = ExpenseCategory.Rent, Amount = 500m },
            new AccountEntry { Type = AccountEntryType.Expense, Category = ExpenseCategory.Rent, Amount = 300m },
            new AccountEntry { Type = AccountEntryType.Income, Category = ExpenseCategory.Other, Amount = 900m }
        });

        var rows = AnalyticsFinanceCalculators.ExpensesByCategory(input);

        Assert.Single(rows);
        Assert.Equal("الإيجار", rows[0].Primary);
        Assert.Contains("800", rows[0].Value);
    }

    [Fact]
    public void ProfitAndLoss_SubtractsTheExpensesFromTheIncome()
    {
        var input = Input(
            new[] { Order("o1", 1000m, _now) },
            entries: new[]
            {
                new AccountEntry { Type = AccountEntryType.Income, Category = ExpenseCategory.Other, Amount = 100m },
                new AccountEntry { Type = AccountEntryType.Expense, Category = ExpenseCategory.Rent, Amount = 400m }
            });

        var rows = AnalyticsFinanceCalculators.ProfitAndLoss(input);

        Assert.Equal(5, rows.Count);
        Assert.Contains("1,000", rows[0].Value);
        Assert.Contains("100", rows[1].Value);
        Assert.Contains("1,100", rows[2].Value);
        Assert.Contains("400", rows[3].Value);
        Assert.Contains("700", rows[4].Value);
    }

    [Fact]
    public void Suppliers_SubtractsThePaymentsFromThePurchases()
    {
        var input = Input(
            suppliers: new[] { new Supplier { Id = "s1", Name = "المورد", IsActive = true } },
            purchaseOrders: new[] { new PurchaseOrder { Id = "po1", SupplierId = "s1", TotalAmount = 900m } },
            payments: new[] { new SupplierPayment { Id = "pay1", SupplierId = "s1", Amount = 400m } });

        var rows = AnalyticsFinanceCalculators.Suppliers(input);

        Assert.Single(rows);
        Assert.Equal("المورد", rows[0].Primary);
        Assert.Contains("500", rows[0].Value);
    }

    [Fact]
    public void Employees_ListsOnlyTheActiveOnes()
    {
        var input = Input(employees: new[]
        {
            new Employee { Id = "e1", Name = "سارة", Position = EmployeePosition.Cashier, MonthlySalary = 500m, IsActive = true },
            new Employee { Id = "e2", Name = "موقوف", Position = EmployeePosition.Cashier, MonthlySalary = 500m, IsActive = false }
        });

        var rows = AnalyticsFinanceCalculators.Employees(input);

        Assert.Single(rows);
        Assert.Equal("سارة", rows[0].Primary);
        Assert.Equal("كاشير", rows[0].Secondary);
    }

    [Fact]
    public void ErrorLog_ReturnsTheNewestEntriesFirst()
    {
        var input = Input(logs: new[]
        {
            new SystemLog { Id = "l1", Timestamp = _now.AddHours(-2), Message = "قديم", Level = "warn" },
            new SystemLog { Id = "l2", Timestamp = _now, Message = "جديد", Level = "error" }
        });

        var rows = AnalyticsFinanceCalculators.ErrorLog(input);

        Assert.Equal(2, rows.Count);
        Assert.Contains("جديد", rows[0].Primary);
        Assert.Equal("ERROR", rows[0].Secondary);
        Assert.Contains("2026/03/10", rows[0].Tertiary);
        Assert.Contains("قديم", rows[1].Primary);
    }

    private static string Classification(IReadOnlyList<Core.Models.Analytics.AnalyticsRow> rows, string name)
    {
        foreach (var row in rows)
        {
            if (row.Primary == name)
                return row.Tertiary.Replace("التصنيف: ", string.Empty).Split('|')[0].Trim();
        }

        return string.Empty;
    }
}
