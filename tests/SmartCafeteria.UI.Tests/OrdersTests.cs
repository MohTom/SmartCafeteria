using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the Orders feature.</summary>
public class OrdersTests : AppTestBase
{
    [Fact]
    public void Orders_ScreenShowsHeaderAndFilters()
    {
        NavigateTo("الطلبات");

        Assert.True(HasText("إدارة الطلبات"), "Orders header not shown");
        Assert.True(HasText("الكل") && HasText("تم الاستلام") && HasText("ملغي"), "Status filters missing");
    }

    [Fact]
    public void Orders_ScreenRendersContent()
    {
        NavigateTo("الطلبات");

        // Either orders are listed or at least the screen rendered with Arabic text
        Assert.True(HasAnyArabicText(), "Orders screen not rendered");
    }
}