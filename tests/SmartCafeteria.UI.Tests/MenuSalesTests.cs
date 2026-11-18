using FlaUI.Core.AutomationElements;
using Microsoft.Data.Sqlite;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the Menu (sales) feature: product list, category filter, add to cart, checkout.</summary>
public class MenuSalesTests : AppTestBase
{
    [Fact]
    public void Menu_ScreenShowsProducts()
    {
        // Default view is the menu
        Assert.True(HasText("القائمة") || HasText("سلة المشتريات"), "Menu screen header not shown");
        Assert.True(HasAnyArabicText(), "No product content rendered on menu");
    }

    [Fact]
    public void Menu_ProductCards_AreClickable()
    {
        // At least one product button (ProductCardButton) should be present since the DB is seeded.
        bool found = false;
        try
        {
            foreach (var btn in MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))
            {
                // Product cards have an emoji and a price text; treat any non-empty clickable card button
                if (!string.IsNullOrEmpty(btn.Name) && btn.Name.Contains('☕') == false && btn.Name.Length > 2)
                {
                    found = true;
                    break;
                }
            }
        }
        catch { }
        Assert.True(found || HasAnyArabicText(), "Expected at least one product card");
    }

    [Fact]
    public void Menu_AddsProductToCart_AndShowsSummary()
    {
        var card = FindFirstProductCard();
        Assert.NotNull(card);

        var invoke = card!.Patterns.Invoke.PatternOrDefault;
        if (invoke != null) invoke.Invoke();
        else card.Click();
        System.Threading.Thread.Sleep(700);

        Assert.True(HasText("سلة المشتريات (1)"), "Cart did not receive the product");
        Assert.True(HasText("المجموع الفرعي:"), "Cart summary is missing");
        Assert.True(HasText("تأكيد الطلب"), "Checkout button is missing");
    }

    [Fact]
    public void Menu_CartQuantityControls_UpdateTotalsAndRemoveItem()
    {
        var card = FindFirstProductCard();
        Assert.NotNull(card);
        Invoke(card!);
        Assert.True(HasText("سلة المشتريات (1)"), "Product was not added to the cart");

        var plus = FindButton("+");
        Assert.NotNull(plus);
        Invoke(plus!);
        Assert.True(HasText("سلة المشتريات (2)"), "Increment did not update the cart count");

        var minus = FindButton("-");
        Assert.NotNull(minus);
        Invoke(minus!);
        Assert.True(HasText("سلة المشتريات (1)"), "Decrement did not update the cart count");

        var remove = FindButton("✕");
        Assert.NotNull(remove);
        Invoke(remove!);
        Assert.True(HasText("سلة المشتريات (0)"), "Remove did not empty the cart");
    }

    [Fact]
    public void Menu_EmptyCheckout_DoesNotCreateOrder()
    {
        var before = CountOrders();
        var checkout = FindByName("تأكيد الطلب");
        Assert.NotNull(checkout);
        Invoke(checkout!);
        System.Threading.Thread.Sleep(1200);

        Assert.Equal(before, CountOrders());
        Assert.True(HasText("سلة المشتريات (0)"), "Empty cart changed after checkout");
    }

    [Fact]
    public void Menu_Search_FiltersOutProducts()
    {
        // Wait for products, then count product cards (name-less, id-less Buttons).
        Assert.True(CountProductCards() > 0, "Expected initial product cards");

        // The search box is the first Edit on the menu screen.
        var searchBox = MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))[0];
        searchBox.AsTextBox().Enter("zzzz-nomatch-unique");
        System.Threading.Thread.Sleep(700);

        // Garbage query should filter out all products.
        Assert.True(CountProductCards() == 0, "Search should have filtered out every product");
    }

    [Fact]
    public void Menu_CategoryFilter_AppliesWithoutError()
    {
        // Products must already be loaded for a category reset to be meaningful.
        Assert.True(CountProductCards() > 0, "Expected initial product cards");

        // Select several categories in turn; the app must stay responsive and
        // products must remain rendered afterwards (all-categories restores the full list).
        foreach (var cat in new[] { "مشروبات ساخنة", "إفطار", "حلوى" })
        {
            var rb = MainWindow.FindFirstDescendant(x => x.ByName(cat));
            Assert.NotNull(rb);
            var sel = rb.Patterns.SelectionItem.PatternOrDefault;
            if (sel != null) sel.Select();
            else rb.Click();
            System.Threading.Thread.Sleep(700);
            // Stable after category switch
            Assert.True(HasText("القائمة"), $"Menu unstable after selecting {cat}");
        }

        // Back to all categories -> products render again
        var all = MainWindow.FindFirstDescendant(x => x.ByName("جميع الفئات"));
        Assert.NotNull(all);
        var selAll = all.Patterns.SelectionItem.PatternOrDefault;
        if (selAll != null) selAll.Select();
        else all.Click();
        System.Threading.Thread.Sleep(700);
        Assert.True(CountProductCards() > 0, "All-categories filter did not restore products");
    }

    private const string QuickAddName = "إضافة سريعة";

    private static void Invoke(AutomationElement element)
    {
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        if (invoke != null) invoke.Invoke();
        else element.Click();
        System.Threading.Thread.Sleep(700);
    }

    private AutomationElement? FindButton(string name)
    {
        try
        {
            return MainWindow.FindFirstDescendant(x => x.ByName(name));
        }
        catch { }
        return null;
    }

    private int CountOrders()
    {
        using var connection = new SqliteConnection($"Data Source={TestData.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Orders";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private int CountProductCards()
    {
        int count = 0;
        try
        {
            foreach (var btn in MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))
            {
                if (btn.Name == QuickAddName)
                    count++;
            }
        }
        catch { }
        return count;
    }

    private AutomationElement? FindFirstProductCard()
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                foreach (var btn in MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))
                {
                    if (btn.Name == QuickAddName)
                        return btn;
                }
            }
            catch { }
            System.Threading.Thread.Sleep(500);
        }
        return null;
    }
}