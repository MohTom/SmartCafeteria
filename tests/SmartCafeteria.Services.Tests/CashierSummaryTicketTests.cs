using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// The cashier copy of a double receipt is the short one: cafeteria name, the
/// receive label, the receipt number, the order type, the meals and the total —
/// nothing else. These tests render it and check both what it prints and what it
/// ignores.
/// </summary>
public class CashierSummaryTicketTests
{
    [Fact]
    public void TheCashierSummary_PrintsTheCafeteriaName()
    {
        var model = BuildSummary();
        var withoutTitle = BuildSummary();
        withoutTitle.Title = string.Empty;

        PrintsSomething(model, withoutTitle);
    }

    [Fact]
    public void TheCashierSummary_PrintsTheReceiveLabel()
    {
        var model = BuildSummary();
        var withoutLabel = BuildSummary();
        withoutLabel.SummaryTitle = string.Empty;

        PrintsSomething(model, withoutLabel);
    }

    [Fact]
    public void TheCashierSummary_PrintsTheReceiptNumber()
    {
        var model = BuildSummary();
        var withoutNumber = BuildSummary();
        withoutNumber.TicketNumber = string.Empty;

        PrintsSomething(model, withoutNumber);
    }

    [Fact]
    public void TheCashierSummary_PrintsTheOrderType()
    {
        var model = BuildSummary();
        var withoutOrderType = BuildSummary();
        withoutOrderType.OrderType = string.Empty;

        PrintsSomething(model, withoutOrderType);
    }

    /// <summary>
    /// On a double receipt the cashier must see for here or to go: the order type
    /// is printed on its own line directly below the receipt number.
    /// </summary>
    [Fact]
    public void TheCashierSummary_PrintsTheOrderTypeBelowTheReceiptNumber()
    {
        var local = BuildSummary();
        var takeaway = BuildSummary();
        takeaway.OrderType = "سفري";
        var otherNumber = BuildSummary();
        otherNumber.TicketNumber = "B-124";

        using var baseImage = TicketRenderHarness.Render(local);
        using var orderTypeImage = TicketRenderHarness.Render(takeaway);
        using var numberImage = TicketRenderHarness.Render(otherNumber);

        var orderType = TicketInk.DiffBounds(baseImage, orderTypeImage);
        var number = TicketInk.DiffBounds(baseImage, numberImage);

        Assert.NotNull(orderType);
        Assert.NotNull(number);

        Assert.True(number!.Value.Bottom < orderType!.Value.Top,
            $"The order type (row {orderType.Value.Top}) is not printed below the receipt number (row {number.Value.Bottom}).");
    }

    [Fact]
    public void TheCashierSummary_PrintsTheMeals()
    {
        var model = BuildSummary();
        var withoutMeals = BuildSummary();
        withoutMeals.Lines.Clear();

        PrintsSomething(model, withoutMeals);
    }

    [Fact]
    public void TheCashierSummary_PrintsTheTotal()
    {
        var model = BuildSummary();
        var withoutTotal = BuildSummary();
        withoutTotal.Total = 0m;

        PrintsSomething(model, withoutTotal);
    }

    [Fact]
    public void TheCashierSummary_IgnoresTheFieldsThatAreNotPartOfIt()
    {
        var model = BuildSummary();

        var withExtraFields = BuildSummary();
        withExtraFields.Subtitle = "شارع النيل الخرطوم";
        withExtraFields.Phone = "TEL: +249 123 456 789";
        withExtraFields.Date = "2026/09/03 10:30";
        withExtraFields.PaymentMethod = "نقداً";
        withExtraFields.PickupTime = "12:45";
        withExtraFields.CustomerName = "محمد علي";
        withExtraFields.CustomerPhone = "0912345678";
        withExtraFields.OrderId = "ORD-1042";

        using var actual = TicketRenderHarness.Render(withExtraFields);
        using var expected = TicketRenderHarness.Render(model);

        Assert.Null(TicketInk.DiffBounds(actual, expected));
    }

    [Fact]
    public void TheCashierSummary_KeepsTheTotalInsideThePage_AtMaxFontSize()
    {
        var model = BuildSummary();

        using var image = TicketRenderHarness.Render(
            model,
            TicketPrintConfig.MaxFontSize,
            TicketRenderHarness.PageHeight * 2);

        var lastInk = TicketInk.LastInkRow(image);
        Assert.True(lastInk < TicketRenderHarness.PageHeight,
            $"The total was cut off at font size {TicketPrintConfig.MaxFontSize} (last ink row {lastInk}).");
    }

    private static void PrintsSomething(TicketPrintModel model, TicketPrintModel without)
    {
        using var actual = TicketRenderHarness.Render(model);
        using var expected = TicketRenderHarness.Render(without);

        Assert.NotNull(TicketInk.DiffBounds(actual, expected));
    }

    private static TicketPrintModel BuildSummary()
    {
        return PrintReceiptBuilder.BuildCashierSummaryReceipt(CreateOrder(), CreateSettings());
    }

    private static Order CreateOrder()
    {
        return new Order
        {
            Id = "0000000000000001",
            Code = "B-123",
            Timestamp = new DateTime(2026, 9, 3, 10, 30, 0),
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
                    Quantity = 2
                },
                new OrderItem
                {
                    Id = "oi2",
                    OrderId = "0000000000000001",
                    ProductId = "p2",
                    ProductNameAr = "كرواسان",
                    ProductPrice = 5,
                    ProductCategory = "Breakfast",
                    Quantity = 1
                }
            }
        };
    }

    private static AppSettings CreateSettings()
    {
        return new AppSettings
        {
            CafeteriaNameEn = "AL-TAMAYUZ CAFETERIA",
            CafeteriaAddress = "شارع النيل الخرطوم",
            CafeteriaPhone = "+249 123 456 789",
            CurrencyAr = "ج.س"
        };
    }
}
