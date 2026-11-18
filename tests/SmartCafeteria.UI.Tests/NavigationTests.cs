using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers app navigation: main window renders Arabic, nav buttons work.</summary>
public class NavigationTests : AppTestBase
{
    [Fact]
    public void Navigation_MainWindow_ShowsArabicTitle()
    {
        var title = MainWindow.Name;
        Assert.False(string.IsNullOrEmpty(title));
        Assert.Contains(title.ToCharArray(), c => c >= 0x0600 && c <= 0x06FF);
    }

    [Fact]
    public void Navigation_HasNavButtons_WithArabicLabels()
    {
        Assert.True(HasAnyArabicText(), "No Arabic-labeled element found in main window");
    }

    [Fact]
    public void Navigation_NavigatesToAdminOnly_UsersScreen()
    {
        // Admin user should see the Users nav item
        var usersNav = MainWindow.FindFirstDescendant(x => x.ByName("المستخدمون"));
        Assert.NotNull(usersNav);
        usersNav!.Click();
        System.Threading.Thread.Sleep(800);
        Assert.True(HasText("إدارة المستخدمين"), "Users screen not shown after nav click");
    }
}