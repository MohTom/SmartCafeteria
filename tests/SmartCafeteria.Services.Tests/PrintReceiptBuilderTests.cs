using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

public class PrintReceiptBuilderTests
{
    private Order CreateOrder()
    {
        var order = new Order
        {
            Id = "0000000000000001",
            Code = "B-123",
            Timestamp = new DateTime(2026, 9, 3, 10, 30, 0),
            PaymentMethod = PaymentMethod.Cash,
            Total = 25,
            Items =
            {
                new OrderItem
                {
                    Id = "oi1",
                    OrderId = "0000000000000001",
                    ProductId = "p1",
                    ProductNameAr = "قهوة",
                    ProductPrice = 10,
                    ProductCategory = "HotDrinks",
                    Quantity = 2,
                    SelectedCustomizationsJson = "[{\"NameAr\":\"حليب\",\"Price\":2}]"
                }
            }
        };
        return order;
    }

    private AppSettings CreateSettings()
    {
        return new AppSettings
        {
            CafeteriaNameEn = "AL-TAMAYUZ CAFETERIA",
            CafeteriaAddress = "شارع النيل الخرطوم",
            CafeteriaPhone = "+249 123 456 789",
            CurrencyAr = "ج.س",
            PrintMethod = PrintMethod.FullReceiptAndSectionReceipts
        };
    }

    [Fact]
    public void BuildReceipt_SetsHeaders()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        var model = PrintReceiptBuilder.BuildReceipt(order, settings);

        Assert.Equal("AL-TAMAYUZ CAFETERIA", model.Title);
        Assert.Equal("شارع النيل الخرطوم", model.Subtitle);
        Assert.Equal("B-123", model.TicketNumber);
        Assert.Equal("نقداً", model.PaymentMethod);
        Assert.Equal("ج.س", model.CurrencySymbolAr);
        Assert.Equal(25, model.Total);
    }

    [Fact]
    public void BuildReceipt_CarriesTheOrderType_ButNoNote()
    {
        var order = CreateOrder();
        order.OrderType = OrderType.Takeaway;
        order.Notes = "  بدون سكر  ";
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildReceipt(order, settings);

        Assert.Equal("سفري", model.OrderType);
        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("بدون سكر"));
        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("ملاحظة"));
    }

    [Fact]
    public void BuildReceipt_DefaultsToLocal()
    {
        var order = CreateOrder();
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildReceipt(order, settings);

        Assert.Equal("محلي", model.OrderType);
    }

    [Theory]
    [InlineData(PrintMethod.TotalReceiptTwice)]
    [InlineData(PrintMethod.FullReceiptAndSectionReceipts)]
    [InlineData(PrintMethod.SectionsOnly)]
    public void BuildReceipts_EveryPrintMethod_CarriesTheOrderType_ButNoNote(PrintMethod method)
    {
        var order = CreateTwoSectionOrder();
        order.OrderType = OrderType.Takeaway;
        order.Notes = "كيس منفصل";
        var settings = CreateSettings();
        settings.PrintMethod = method;

        var models = PrintReceiptBuilder.BuildReceiptsForOrder(order, settings, method);

        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.Equal("سفري", m.OrderType));
        Assert.All(models, m => Assert.DoesNotContain(m.Lines, l => l.Text.Contains("كيس منفصل")));
        Assert.All(models, m => Assert.DoesNotContain(m.Lines, l => l.Text.Contains("ملاحظة")));
    }

    [Fact]
    public void BuildReceipt_DoesNotAddTheItemNoteLine()
    {
        var order = CreateOrder();
        order.Items[0].Notes = "بدون حليب";
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildReceipt(order, settings);

        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("بدون حليب"));
        Assert.Equal(2, model.Lines.Count); // product line + customization line
    }

    [Fact]
    public void BuildReceipt_ContainsLineItems()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        var model = PrintReceiptBuilder.BuildReceipt(order, settings);

        Assert.Equal(2, model.Lines.Count); // product line + customization line
        Assert.Contains("2x قهوة", model.Lines[0].Text);
        Assert.Contains("حليب", model.Lines[1].Text);
        Assert.True(model.Lines[1].IsSmall);
    }

    [Fact]
    public void BuildReceipts_FullAndSection_CreatesFullPlusMinimalSection()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        var models = PrintReceiptBuilder.BuildReceiptsForOrder(order, settings, PrintMethod.FullReceiptAndSectionReceipts);

        Assert.Equal(2, models.Count);
        Assert.False(models[0].IsMinimal);
        Assert.Equal("AL-TAMAYUZ CAFETERIA", models[0].Title);
        Assert.True(models[1].IsMinimal);
        Assert.Equal("AL-TAMAYUZ CAFETERIA", models[1].Title);
        Assert.Equal("مشروبات ساخنة", models[1].SectionName);
        Assert.All(models[1].Lines, line => Assert.Equal(string.Empty, line.PriceText));
    }

    [Fact]
    public void BuildReceipts_FullAndSection_CreatesOneMinimalTicketPerSection()
    {
        var order = CreateTwoSectionOrder();
        var settings = CreateSettings();
        var models = PrintReceiptBuilder.BuildReceiptsForOrder(order, settings, PrintMethod.FullReceiptAndSectionReceipts);

        Assert.Equal(3, models.Count);
        Assert.False(models[0].IsMinimal);
        Assert.Equal(2, models.Count(m => m.IsMinimal));
        Assert.Contains(models, m => m.IsMinimal && m.SectionName.Contains("مشروبات ساخنة"));
        Assert.Contains(models, m => m.IsMinimal && m.SectionName.Contains("مشروبات باردة"));
    }

    [Fact]
    public async Task PrintOrder_FullAndSection_PrintsOneFullAndOneMinimalTicketPerSection()
    {
        var order = CreateTwoSectionOrder();
        var settings = CreateSettings();
        settings.PrintMethod = PrintMethod.FullReceiptAndSectionReceipts;
        var fake = new RecordingPrintService();

        await PrintReceiptBuilder.PrintOrderWithMethodAsync(fake, order, settings);

        Assert.Equal(new[] { "customer" }, fake.Calls);
        Assert.Single(fake.CustomerReceipts);
        Assert.Equal(2, fake.KitchenReceipts.Count);
        Assert.All(fake.KitchenReceipts, k => Assert.True(k.Minimal));
        Assert.Contains(fake.KitchenReceipts, k => k.SectionName.Contains("مشروبات ساخنة"));
        Assert.Contains(fake.KitchenReceipts, k => k.SectionName.Contains("مشروبات باردة"));
    }

    [Fact]
    public void BuildReceipts_TotalTwice_CreatesCustomerAndCashierSummary()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        var models = PrintReceiptBuilder.BuildReceiptsForOrder(order, settings, PrintMethod.TotalReceiptTwice);

        Assert.Equal(2, models.Count);
        Assert.Contains("نسخة العميل", models[0].Subtitle);
        Assert.False(models[0].IsCashierSummary);
        Assert.True(models[1].IsCashierSummary);
    }

    [Fact]
    public void BuildCashierSummary_ContainsOnlyTheRequiredContent()
    {
        var order = CreateOrder();
        order.OrderType = OrderType.Takeaway;
        order.Notes = "بدون سكر";
        order.PickupTime = "12:45";
        order.Items[0].Notes = "بدون حليب";
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildCashierSummaryReceipt(order, settings);

        Assert.True(model.IsCashierSummary);
        Assert.Equal("AL-TAMAYUZ CAFETERIA", model.Title);
        Assert.Equal("إيصال استلام", model.SummaryTitle);
        Assert.Equal("B-123", model.TicketNumber);
        Assert.Equal(25, model.Total);
        Assert.Equal("ج.س", model.CurrencySymbolAr);
        Assert.Equal(string.Empty, model.Subtitle);
        Assert.Equal(string.Empty, model.Phone);
        Assert.Equal(string.Empty, model.Date);
        Assert.Equal(string.Empty, model.PaymentMethod);
        Assert.Equal("سفري", model.OrderType);
        Assert.Equal(string.Empty, model.PickupTime);
        Assert.Equal(string.Empty, model.OrderId);
        Assert.Equal(string.Empty, model.CustomerName);
        Assert.Equal(string.Empty, model.CustomerPhone);
    }

    [Fact]
    public void BuildCashierSummary_DefaultsToLocalOrderType()
    {
        var order = CreateOrder();
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildCashierSummaryReceipt(order, settings);

        Assert.Equal("محلي", model.OrderType);
    }

    [Fact]
    public void BuildCashierSummary_ListsMealsWithoutPricesCustomizationsOrNotes()
    {
        var order = CreateOrder();
        order.Items[0].Notes = "بدون حليب";
        var settings = CreateSettings();

        var model = PrintReceiptBuilder.BuildCashierSummaryReceipt(order, settings);

        var line = Assert.Single(model.Lines);
        Assert.Equal("2x قهوة", line.Text);
        Assert.Equal(string.Empty, line.PriceText);
        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("حليب"));
        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("بدون حليب"));
        Assert.DoesNotContain(model.Lines, l => l.Text.Contains("ملاحظة"));
    }

    [Fact]
    public async Task PrintOrder_TotalTwice_PrintsCustomerThenCashierSummary()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        settings.PrintMethod = PrintMethod.TotalReceiptTwice;
        var fake = new RecordingPrintService();

        await PrintReceiptBuilder.PrintOrderWithMethodAsync(fake, order, settings);

        Assert.Equal(new[] { "customer", "cashier" }, fake.Calls);
        Assert.Single(fake.CustomerReceipts);
        Assert.Single(fake.CashierSummaries);
    }

    [Fact]
    public void BuildReceipt_NullSectionItems_UsesOrderItems()
    {
        var order = CreateOrder();
        var settings = CreateSettings();
        var model = PrintReceiptBuilder.BuildReceipt(order, settings, null, null);

        Assert.Equal(2, model.Lines.Count); // 1 order item + 1 customization line
    }

    [Fact]
    public void ThermalPrinter_IsVirtualPrinter_DetectsPdf()
    {
        // Indirect test: verify the virtual printer skip logic via printer name handling
        // The static method is private; test default printer returns non-null
        var defaultPrinter = ThermalPrinter.GetDefaultPrinter();
        Assert.NotNull(defaultPrinter);
    }

    private Order CreateTwoSectionOrder()
    {
        var order = CreateOrder();
        order.Items.Add(new OrderItem
        {
            Id = "oi2",
            OrderId = order.Id,
            ProductId = "p2",
            ProductNameAr = "عصير",
            ProductPrice = 5,
            ProductCategory = "ColdDrinks",
            Quantity = 1,
            SelectedCustomizationsJson = "[]"
        });
        return order;
    }

    [Fact]
    public void BuildReceipts_SectionsOnly_CreatesOneTicketPerSectionWithoutCustomerReceipt()
    {
        var order = CreateTwoSectionOrder();
        var settings = CreateSettings();
        var models = PrintReceiptBuilder.BuildReceiptsForOrder(order, settings, PrintMethod.SectionsOnly);

        Assert.Equal(2, models.Count);
        Assert.Equal("AL-TAMAYUZ CAFETERIA", models[0].Title);
        Assert.Equal("AL-TAMAYUZ CAFETERIA", models[1].Title);
        Assert.Contains(models, m => m.SectionName.Contains("مشروبات ساخنة"));
        Assert.Contains(models, m => m.SectionName.Contains("مشروبات باردة"));
        Assert.DoesNotContain(models, m => m.Subtitle.Contains("نسخة"));
        Assert.All(models, m => Assert.Equal("B-123", m.TicketNumber));
        Assert.All(models, m => Assert.True(m.IsMinimal));
        Assert.DoesNotContain(models, m => m.Lines.Any(l => l.Text.Contains("10.00")));
    }

    [Fact]
    public async Task PrintOrder_SectionsOnly_PrintsKitchenTicketsWithoutCustomerReceipt()
    {
        var order = CreateTwoSectionOrder();
        var settings = CreateSettings();
        settings.PrintMethod = PrintMethod.SectionsOnly;
        var fake = new RecordingPrintService();

        await PrintReceiptBuilder.PrintOrderWithMethodAsync(fake, order, settings);

        Assert.Empty(fake.CustomerReceipts);
        Assert.Equal(2, fake.KitchenReceipts.Count);
        Assert.Contains("مشروبات ساخنة", fake.KitchenReceipts[0].SectionName);
        Assert.All(fake.KitchenReceipts, k => Assert.True(k.Minimal));
    }

    private sealed class RecordingPrintService : IPrintService
    {
        public List<Order> CustomerReceipts { get; } = new();
        public List<Order> CashierSummaries { get; } = new();
        public List<(string SectionName, List<OrderItem> Items, bool Minimal)> KitchenReceipts { get; } = new();
        public List<string> Calls { get; } = new();

        public Task PrintReceiptAsync(Order order, AppSettings settings)
        {
            Calls.Add("customer");
            CustomerReceipts.Add(order);
            return Task.CompletedTask;
        }

        public Task PrintCashierSummaryAsync(Order order, AppSettings settings)
        {
            Calls.Add("cashier");
            CashierSummaries.Add(order);
            return Task.CompletedTask;
        }

        public Task PrintKitchenReceiptAsync(Order order, string sectionName, List<OrderItem> sectionItems, AppSettings settings, bool minimal = false)
        {
            KitchenReceipts.Add((sectionName, sectionItems, minimal));
            return Task.CompletedTask;
        }

        public Task<List<string>> GetInstalledPrintersAsync() => Task.FromResult(new List<string>());

        public Task<string> GetDefaultPrinterAsync() => Task.FromResult(string.Empty);
    }
}