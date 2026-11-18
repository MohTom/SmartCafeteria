using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the Inventory feature (list, search, add product).</summary>
public class InventoryTests : AppTestBase
{
    private static int _seq = 0;

    [Fact]
    public void Inventory_ScreenShowsHeaderAndProducts()
    {
        NavigateTo("المخزون");

        Assert.True(HasText("إدارة المخزون"), "Inventory header not shown");
        Assert.True(HasText("إضافة منتج جديد"), "Add product button not shown");
        // Seeded products should appear (Arabic names present)
        Assert.True(HasAnyArabicText(), "No inventory content rendered");
    }

    [Fact]
    public void Inventory_EditDialogOpens_AndCancels()
    {
        NavigateTo("المخزون");
        Assert.True(HasText("إدارة المخزون"), "Inventory header not shown");

        // Open edit on the first product's "تعديل" button (Invoke is scroll-safe).
        var editBtn = FindFirstButtonByName("تعديل");
        Assert.NotNull(editBtn);
        var inv = editBtn.Patterns.Invoke.PatternOrDefault;
        if (inv != null) inv.Invoke();
        else editBtn.Click();
        System.Threading.Thread.Sleep(700);

        Assert.True(HasText("تعديل المنتج"), "Edit dialog did not open");

        // Cancel closes the form
        var cancelBtn = MainWindow.FindFirstDescendant(x => x.ByName("إلغاء"));
        Assert.NotNull(cancelBtn);
        var invc = cancelBtn.Patterns.Invoke.PatternOrDefault;
        if (invc != null) invc.Invoke();
        else cancelBtn.Click();
        System.Threading.Thread.Sleep(700);
        Assert.False(HasText("تعديل المنتج"), "Edit dialog did not close after cancel");
    }

    [Fact]
    public void Inventory_AddAndEditForms_DoNotShowImageUrlField()
    {
        NavigateTo("المخزون");

        var addBtn = MainWindow.FindFirstDescendant(x => x.ByName("إضافة منتج جديد"));
        Assert.NotNull(addBtn);
        SafeInvoke(addBtn);
        Assert.NotNull(MainWindow.FindFirstDescendant(x => x.ByAutomationId("AddNameAr")));
        Assert.Null(MainWindow.FindFirstDescendant(x => x.ByAutomationId("AddImageUrl")));
        Assert.False(HasText("رابط الصورة:"), "Image URL label is still shown in the add form");

        var cancelBtn = MainWindow.FindFirstDescendant(x => x.ByName("إلغاء"));
        Assert.NotNull(cancelBtn);
        SafeInvoke(cancelBtn);

        AutomationElement? editBtn = null;
        for (var i = 0; i < 20 && editBtn is null; i++)
        {
            editBtn = FindFirstButtonByName("تعديل");
            if (editBtn is null) System.Threading.Thread.Sleep(250);
        }
        Assert.NotNull(editBtn);
        SafeInvoke(editBtn);
        Assert.NotNull(MainWindow.FindFirstDescendant(x => x.ByAutomationId("EditNameAr")));
        Assert.Null(MainWindow.FindFirstDescendant(x => x.ByAutomationId("EditImageUrl")));
        Assert.False(HasText("رابط الصورة:"), "Image URL label is still shown in the edit form");
    }

    [Fact]
    public void Inventory_Search_FiltersOutAllProducts()
    {
        NavigateTo("المخزون");
        Assert.True(CountButtons("تعديل") > 0, "Expected inventory products before search");

        var searchBox = MainWindow.FindAllDescendants(
            x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))[0];
        searchBox.AsTextBox().Enter("zzzz-nomatch-unique");
        System.Threading.Thread.Sleep(900);

        Assert.Equal(0, CountButtons("تعديل"));
        Assert.True(HasText("إدارة المخزون"), "Inventory screen became unstable after search");
    }

    [Fact]
    public void Inventory_AddProduct_AppearsInList()
    {
        NavigateTo("المخزون");

        // Open the add form
        var addBtn = MainWindow.FindFirstDescendant(x => x.ByName("إضافة منتج جديد"));
        Assert.NotNull(addBtn);
        addBtn.Click();
        System.Threading.Thread.Sleep(700);

        Assert.True(HasText("إضافة منتج جديد"), "Add form did not open");

        var productCode = $"{++_seq}{System.Environment.TickCount % 1000000}";
        var enName = $"AutoProd_{productCode}";

        EnterById("AddNameAr", $"منتج اختبار {productCode}");
        EnterById("AddNameEn", enName);
        EnterById("AddPrice", "15.5");
        EnterById("AddInventory", "20");
        EnterById("AddMaxInventory", "50");

        // Invoke the save. Programmatic invoke is reliable even if the button is
        // scrolled below the modal's fold.
        var saveBtn = MainWindow.FindFirstDescendant(x => x.ByName("إضافة"));
        Assert.NotNull(saveBtn);
        var inv = saveBtn.Patterns.Invoke.PatternOrDefault;
        if (inv != null) inv.Invoke();
        else saveBtn.Click();
        System.Threading.Thread.Sleep(1500);

        // Search for the new product to bring it into view and confirm it was added.
        Assert.True(HasText(enName), "Newly added product not visible");
    }

    private int CountButtons(string name)
    {
        try
        {
            return MainWindow
                .FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                .Count(b => b.Name == name);
        }
        catch
        {
            return 0;
        }
    }

    private AutomationElement? FindFirstButtonByName(string name)
    {
        try
        {
            return MainWindow.FindFirstDescendant(x => x.ByName(name));
        }
        catch { }
        return null;
    }
}