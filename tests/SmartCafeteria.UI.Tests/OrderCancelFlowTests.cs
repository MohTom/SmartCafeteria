using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the order cancel flow through the real UI: creates its own
/// order via the repo, cancels it with the order-card cancel button, then
/// deletes it with the admin delete button. Net database effect: zero.</summary>
public class OrderCancelFlowTests : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly UiTestDatabase _testData;
    private readonly Application _app;
    private readonly UIA3Automation _automation;
    private readonly Window _mainWindow;

    public OrderCancelFlowTests(ITestOutputHelper output)
    {
        _out = output;
        _testData = UiTestDatabase.Create();

        var baseDir = AppContext.BaseDirectory;
        var exes = Directory.GetFiles(baseDir, "SmartCafeteria.exe", SearchOption.AllDirectories);
        Assert.NotEmpty(exes);

        _automation = new UIA3Automation();
        _app = TestAppProcessGuard.Instance.Launch(exes[0]);

        var loginWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(30));
        Assert.NotNull(loginWindow);
        loginWindow.FindFirstDescendant(x => x.ByControlType(ControlType.Edit)).AsTextBox().Enter(UiTestDatabase.AdminUsername);
        loginWindow.FindFirstDescendant(x => x.ByAutomationId("PasswordInput")).AsTextBox().Enter(UiTestDatabase.AdminPassword);
        loginWindow.FindFirstDescendant(x => x.ByName("دخول")).Click();

        Window? main = null;
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(500);
            try
            {
                var w = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(3));
                if (w != null && !string.IsNullOrEmpty(w.Name)) { main = w; break; }
            }
            catch { }
        }
        Assert.NotNull(main);
        _mainWindow = main!;
    }

    public void Dispose()
    {
        TestAppProcessGuard.Instance.Release(_app);
        _automation?.Dispose();
        _testData.Dispose();
    }

    private void ClickByName(string name, int waitMs = 900)
    {
        var el = _mainWindow.FindFirstDescendant(x => x.ByName(name));
        Assert.NotNull(el);
        var inv = el!.Patterns.Invoke.PatternOrDefault;
        if (inv != null) inv.Invoke(); else el.Click();
        Thread.Sleep(waitMs);
    }

    private bool HasText(string text)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (_mainWindow.FindFirstDescendant(x => x.ByName(text)) != null)
                    return true;
            }
            catch { }
            Thread.Sleep(400);
        }
        return false;
    }

    [Fact]
    public async Task CancelButton_CancelsOrder_ThenAdminDelete_RemovesIt()
    {
        // Arrange: create an order directly in the database.
        var ctx = new DbContext();
        var productRepo = new ProductRepository(ctx);
        var products = (await productRepo.GetAllProductsAsync()).Where(p => p.Inventory > 0).ToList();
        Assert.NotEmpty(products);

        var orderRepo = new OrderRepository(ctx, productRepo);
        var probe = await orderRepo.CreateOrderAsync(
            new List<CartItem> { new CartItem { Product = products[0], Quantity = 1 } },
            PaymentMethod.Cash, "TEST", "test", false, null);
        _out.WriteLine($"PROBE ORDER: {probe.Code} id={probe.Id}");

        // Act: cancel it with the real order-card cancel button (newest card first).
        ClickByName("الطلبات", 1200);

        var cancelButtons = _mainWindow.FindAllDescendants(x => x.ByControlType(ControlType.Button))
            .Where(b => b.Name == "إلغاء").ToList();
        Assert.NotEmpty(cancelButtons);
        var inv = cancelButtons[0].Patterns.Invoke.PatternOrDefault;
        if (inv != null) inv.Invoke(); else cancelButtons[0].Click();
        Thread.Sleep(1500);

        // Assert: cancelled in the database and gone from the default received filter.
        var ctx2 = new DbContext();
        var reloaded = await new OrderRepository(ctx2, new ProductRepository(ctx2)).GetOrderByIdAsync(probe.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(OrderStatus.Cancelled, reloaded.Status);

        // The cancelled order is visible under the cancelled filter.
        ClickByName("ملغي", 900);
        Assert.True(HasText(probe.Code), "Cancelled order not shown under the cancelled filter");

        // Cleanup through the real admin UI: permanent delete + confirmation.
        ClickByName("حذف نهائي", 500);
        var dialog = _automation.GetDesktop()
            .FindFirstDescendant(x => x.ByName("تأكيد الحذف"));
        Assert.NotNull(dialog);
        var yes = dialog!.FindAllDescendants(x => x.ByControlType(ControlType.Button))
            .FirstOrDefault(b => b.Name is "نعم" or "Yes" or "&Yes");
        Assert.NotNull(yes);
        var yesInvoke = yes!.Patterns.Invoke.PatternOrDefault;
        if (yesInvoke != null) yesInvoke.Invoke(); else yes.Click();
        Thread.Sleep(1500);

        var ctx3 = new DbContext();
        Assert.Null(await new OrderRepository(ctx3, new ProductRepository(ctx3)).GetOrderByIdAsync(probe.Id));
    }
}
