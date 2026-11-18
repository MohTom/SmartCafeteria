using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// The order note and the item notes belong to the order, the screens and the
/// reports, never to a printed ticket. These tests render the same order with and
/// without notes: the ink has to stay byte-identical on every ticket kind.
/// </summary>
public class TicketNotesRemovalTests
{
    [Fact]
    public void TheCustomerReceipt_DoesNotPrintTheOrderNote()
    {
        var (withNote, withoutNote) = BuildCustomerReceipts(order =>
        {
            order.Notes = "بدون سكر";
        });

        using var actual = TicketRenderHarness.Render(withNote);
        using var expected = TicketRenderHarness.Render(withoutNote);

        Assert.Null(TicketInk.DiffBounds(actual, expected));
    }

    [Fact]
    public void TheSectionTicket_DoesNotPrintTheOrderNote()
    {
        var (withNote, withoutNote) = BuildSectionTickets(order =>
        {
            order.Notes = "تغليف منفصل";
        });

        using var actual = TicketRenderHarness.Render(withNote);
        using var expected = TicketRenderHarness.Render(withoutNote);

        Assert.Null(TicketInk.DiffBounds(actual, expected));
    }

    [Fact]
    public void TheCustomerReceipt_DoesNotPrintTheItemNote()
    {
        var (withNote, withoutNote) = BuildCustomerReceipts(order =>
        {
            order.Items[0].Notes = "بدون حليب";
        });

        using var actual = TicketRenderHarness.Render(withNote);
        using var expected = TicketRenderHarness.Render(withoutNote);

        Assert.Null(TicketInk.DiffBounds(actual, expected));
    }

    [Fact]
    public void TheSectionTicket_DoesNotPrintTheItemNote()
    {
        var (withNote, withoutNote) = BuildSectionTickets(order =>
        {
            order.Items[0].Notes = "بدون سكر";
        });

        using var actual = TicketRenderHarness.Render(withNote);
        using var expected = TicketRenderHarness.Render(withoutNote);

        Assert.Null(TicketInk.DiffBounds(actual, expected));
    }

    private static (TicketPrintModel WithNote, TicketPrintModel WithoutNote) BuildCustomerReceipts(
        Action<Order> addNote)
    {
        var settings = CreateSettings();
        var withoutNote = PrintReceiptBuilder.BuildReceipt(CreateOrder(), settings);

        var order = CreateOrder();
        addNote(order);
        var withNote = PrintReceiptBuilder.BuildReceipt(order, settings);

        return (withNote, withoutNote);
    }

    private static (TicketPrintModel WithNote, TicketPrintModel WithoutNote) BuildSectionTickets(
        Action<Order> addNote)
    {
        var settings = CreateSettings();
        var items = CreateOrder().Items;

        var withoutNote = PrintReceiptBuilder.BuildReceipt(
            CreateOrder(), settings, "مشروبات ساخنة", items, minimal: true);

        var order = CreateOrder();
        addNote(order);
        var withNote = PrintReceiptBuilder.BuildReceipt(
            order, settings, "مشروبات ساخنة", order.Items, minimal: true);

        return (withNote, withoutNote);
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
                    Quantity = 2,
                    SelectedCustomizationsJson = "[]"
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
