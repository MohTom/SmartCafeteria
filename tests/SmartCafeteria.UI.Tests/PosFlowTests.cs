using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>
/// Covers the improved POS flow: quick-add from the menu using the explicit quick-add
/// affordance on each product card, cart count update, and checkout receipt.
/// </summary>
public class PosFlowTests : AppTestBase
{
    private AutomationElement[] ProductCards()
    {
        var buttons = MainWindow.FindAllDescendants(x => x.ByControlType(ControlType.Button));
        return buttons.Where(b => b.Name == "إضافة سريعة").ToArray();
    }

    [Fact]
    public void Menu_QuickAdd_UpdatesCartCount()
    {
        Assert.True(ProductCards().Length > 0, "No product cards shown");

        var quickAdd = FindByName("إضافة سريعة");
        Assert.NotNull(quickAdd);

        quickAdd!.Click();
        System.Threading.Thread.Sleep(700);

        Assert.True(HasText("سلة المشتريات (1)"), "Cart count did not update after quick add");
    }

    private ComboBox OrderTypeCombo()
    {
        var combo = MainWindow.FindFirstDescendant(x => x.ByAutomationId("OrderTypeCombo"));
        Assert.NotNull(combo);
        return combo.AsComboBox();
    }

    private static string SelectedText(ComboBox combo)
    {
        var selected = combo.SelectedItem?.Name;
        var text = string.IsNullOrEmpty(selected) ? combo.Value : selected;
        return text switch
        {
            "Local" => "محلي",
            "Takeaway" => "سفري",
            _ => text
        };
    }

    private void SelectOrderType(string label)
    {
        var combo = OrderTypeCombo();
        combo.Expand();
        System.Threading.Thread.Sleep(400);

        var aliases = label == "سفري" ? new[] { "سفري", "Takeaway" } : new[] { label, "Local" };
        var item = combo.Items.FirstOrDefault(i => i.Name != null && aliases.Any(a => i.Name.Contains(a)))
                   ?? MainWindow.FindAllDescendants(x => x.ByControlType(ControlType.ListItem))
                       .FirstOrDefault(i => i.Name != null && aliases.Any(a => i.Name.Contains(a)));

        Assert.True(item != null, $"Order type option '{label}' not found");
        item!.Click();
        System.Threading.Thread.Sleep(400);
    }

    [Fact]
    public void Menu_OrderType_DefaultsToLocal()
    {
        Assert.True(OrderTypeCombo().IsEnabled, "Order type combo is not enabled");
        Assert.Equal("محلي", SelectedText(OrderTypeCombo()));
    }

    [Fact]
    public void Menu_OrderType_CanBeSetToTakeaway()
    {
        SelectOrderType("سفري");

        Assert.Equal("سفري", SelectedText(OrderTypeCombo()));
    }

    [Fact]
    public void Menu_OrderNote_AcceptsText()
    {
        EnterById("OrderNotesInput", "بدون سكر");

        var notes = MainWindow.FindFirstDescendant(x => x.ByAutomationId("OrderNotesInput"));
        Assert.NotNull(notes);
        Assert.Equal("بدون سكر", notes.AsTextBox().Text);
    }

    [Fact]
    public void Menu_OrderFields_AreCompactButReadable()
    {
        var reference = FindById("SearchBox");
        Assert.NotNull(reference);
        var referenceHeight = reference!.BoundingRectangle.Height;

        foreach (var id in new[] { "PaymentMethodCombo", "OrderTypeCombo", "OrderNotesInput" })
        {
            var element = FindById(id);
            Assert.NotNull(element);

            Assert.True(element!.BoundingRectangle.Height < referenceHeight,
                $"{id} is not compact: {element.BoundingRectangle.Height} >= {referenceHeight}.");
        }

        Assert.Equal("محلي", SelectedText(OrderTypeCombo()));

        EnterById("OrderNotesInput", "تغليف هدية");
        var notes = FindById("OrderNotesInput");
        Assert.NotNull(notes);
        Assert.Equal("تغليف هدية", notes!.AsTextBox().Text);
    }

    private AutomationElement? FindById(string automationId)
    {
        try
        {
            return MainWindow.FindFirstDescendant(x => x.ByAutomationId(automationId));
        }
        catch { }
        return null;
    }

    [Fact]
    public void Menu_Checkout_ResetsTheOrderTypeAndTheNote()
    {
        Assert.True(ProductCards().Length > 0, "No product cards shown");

        SelectOrderType("سفري");
        EnterById("OrderNotesInput", "تغليف هدية");

        SafeInvoke(FindByName("إضافة سريعة"), 700);
        SafeInvoke(FindByName("تأكيد الطلب"), 2000);

        Assert.True(HasText("سلة المشتريات (0)"), "Cart did not clear after checkout");
        Assert.Equal("محلي", SelectedText(OrderTypeCombo()));

        var notes = MainWindow.FindFirstDescendant(x => x.ByAutomationId("OrderNotesInput"));
        Assert.NotNull(notes);
        Assert.Equal(string.Empty, notes.AsTextBox().Text);
    }

    [Fact]
    public void Menu_QuickAdd_ThenCheckout_ClearsCart()
    {
        Assert.True(ProductCards().Length > 0, "No product cards shown");

        var quickAdd = FindByName("إضافة سريعة");
        Assert.NotNull(quickAdd);

        quickAdd!.Click();
        System.Threading.Thread.Sleep(700);
        Assert.True(HasText("سلة المشتريات (1)"), "Quick add did not populate the cart");

        SafeInvoke(FindByName("تأكيد الطلب"), 2000);
        Assert.True(HasText("سلة المشتريات (0)"), "Cart was not cleared after checkout");
    }
}
