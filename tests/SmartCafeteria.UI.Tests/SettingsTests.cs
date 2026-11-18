using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the Settings feature.</summary>
public class SettingsTests : AppTestBase
{
    [Fact]
    public void Settings_ScreenShowsSections()
    {
        NavigateTo("الإعدادات");

        Assert.True(HasText("الإعدادات"), "Settings header not shown");
        Assert.True(HasText("الإعدادات العامة"), "General settings section missing");
        Assert.True(HasText("إعدادات الطباعة"), "Printing settings section missing");
        Assert.True(HasText("معلومات الكافتيريا"), "Cafeteria info section missing");
        Assert.True(HasText("حفظ الإعدادات"), "Save button missing");
    }

    [Fact]
    public void Settings_SaveSettings_ShowsSuccessStatus()
    {
        NavigateTo("الإعدادات");

        // Click save (holding current values). Use programmatic invoke so it works
        // even if the button is scrolled out of the visible viewport.
        var saveBtn = MainWindow.FindFirstDescendant(x => x.ByName("حفظ الإعدادات"));
        Assert.NotNull(saveBtn);
        var invoke = saveBtn.Patterns.Invoke.PatternOrDefault;
        if (invoke != null)
        {
            invoke.Invoke();
        }
        else
        {
            saveBtn.Click();
        }
        System.Threading.Thread.Sleep(1500);

        Assert.True(HasText("تم حفظ الإعدادات بنجاح"), "Save status message not shown");
    }

    [Fact]
    public void Settings_TicketFontSize_SliderIsPresent()
    {
        NavigateTo("الإعدادات");

        Assert.True(HasText("حجم خط الإيصال:"), "Ticket font size label missing");

        var slider = MainWindow.FindFirstDescendant(x => x.ByAutomationId("TicketFontSizeSlider"));
        Assert.NotNull(slider);
    }
}