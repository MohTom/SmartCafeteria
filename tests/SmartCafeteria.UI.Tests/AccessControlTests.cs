using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers role-based navigation visibility and session logout.</summary>
public class CashierAccessTests : AppTestBase
{
    public CashierAccessTests()
        : base(loginUsername: UiTestDatabase.CashierUsername,
               loginPassword: UiTestDatabase.CashierPassword)
    {
    }

    [Fact]
    public void Cashier_HidesAdminOnlyNavigation()
    {
        Assert.True(HasText("القائمة"), "Cashier did not land on the menu screen");
        Assert.NotNull(FindByName("الطلبات"));
        Assert.NotNull(FindByName("المخزون"));

        foreach (var adminNav in new[] { "التقارير", "المحاسبة", "الموردون", "الموظفون", "المستخدمون", "الإعدادات" })
            Assert.Null(FindByName(adminNav));
    }
}

public class AccessControlTests : AppTestBase
{
    [Fact]
    public void Logout_ReturnsToLoginWindow()
    {
        var logout = FindByName("تسجيل الخروج");
        Assert.NotNull(logout);
        SafeInvoke(logout);

        Window? login = null;
        for (var i = 0; i < 20 && login == null; i++)
        {
            System.Threading.Thread.Sleep(500);
            try
            {
                var window = App.GetMainWindow(Automation, TimeSpan.FromSeconds(3));
                if (window?.Name == "تسجيل الدخول")
                    login = window;
            }
            catch { }
        }

        Assert.NotNull(login);
        Assert.NotNull(login!.FindFirstDescendant(x => x.ByAutomationId("PasswordInput")));
    }
}
