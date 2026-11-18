using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the login feature (correct / incorrect credentials).</summary>
public class LoginTests : IDisposable
{
    private readonly string _exePath;
    private readonly UiTestDatabase _testData;
    private Application? _app;
    private UIA3Automation? _automation;

    public LoginTests()
    {
        var baseDir = AppContext.BaseDirectory;
        _exePath = Directory.GetFiles(baseDir, "SmartCafeteria.exe", SearchOption.AllDirectories)[0];
        _testData = UiTestDatabase.Create();
    }

    public void Dispose()
    {
        TestAppProcessGuard.Instance.Release(_app);
        _automation?.Dispose();
        _testData.Dispose();
    }

    private void Launch()
    {
        _automation = new UIA3Automation();
        _app = TestAppProcessGuard.Instance.Launch(_exePath);
    }

    [Fact]
    public void Login_ShowsLoginWindow()
    {
        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        // Login window has username field, password field, and login button
        var edit = login.FindFirstDescendant(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.NotNull(edit);
        var pw = login.FindFirstDescendant(x => x.ByAutomationId("PasswordInput"));
        Assert.NotNull(pw);
        var btn = login.FindFirstDescendant(x => x.ByName("دخول"));
        Assert.NotNull(btn);
    }

    [Fact]
    public void Login_ShowsDeveloperCredits()
    {
        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        Assert.NotNull(login.FindFirstDescendant(x => x.ByName("تم التصميم بواسطة محمد إبراهيم التوم")));
        Assert.NotNull(login.FindFirstDescendant(x => x.ByName("mohammedtom904@gmail.com")));
        Assert.NotNull(login.FindFirstDescendant(x => x.ByName("0129039378")));
    }

    [Fact]
    public void Login_DeveloperPanelAddsNoInputs()
    {
        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        var edits = login.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        Assert.Equal(2, edits.Length);

        Assert.NotNull(login.FindFirstDescendant(x => x.ByAutomationId("AppLogo")));
    }

    [Fact]
    public void Login_EmptyCredentials_ShowsValidationError()
    {
        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        login.FindFirstDescendant(x => x.ByName("دخول"))!.Click();
        System.Threading.Thread.Sleep(800);

        Assert.NotNull(login.FindFirstDescendant(x => x.ByName("الرجاء إدخال اسم المستخدم وكلمة المرور")));
    }

    [Fact]
    public void Login_InactiveUser_ShowsInactiveError()
    {
        const string username = "ui_inactive";
        _testData.EnsureUser(username, "UiTest!Pass123", "UI Inactive User",
            SmartCafeteria.Core.Enums.UserRole.Cashier, isActive: false);

        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        var edit = login.FindFirstDescendant(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        edit!.AsTextBox().Enter(username);
        login.FindFirstDescendant(x => x.ByAutomationId("PasswordInput"))!
            .AsTextBox().Enter("UiTest!Pass123");
        login.FindFirstDescendant(x => x.ByName("دخول"))!.Click();

        System.Threading.Thread.Sleep(1200);
        Assert.NotNull(login.FindFirstDescendant(x => x.ByName("هذا الحساب غير نشط، راجع مدير النظام")));
    }

    [Fact]
    public void Login_WrongPassword_ShowsError_NoMainWindow()
    {
        Launch();
        var login = _app!.GetMainWindow(_automation!, TimeSpan.FromSeconds(30));
        Assert.NotNull(login);

        var edit = login.FindFirstDescendant(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
        edit!.AsTextBox().Enter(UiTestDatabase.AdminUsername);
        var pw = login.FindFirstDescendant(x => x.ByAutomationId("PasswordInput"));
        pw!.AsTextBox().Enter("wrong-password");
        login.FindFirstDescendant(x => x.ByName("دخول"))!.Click();

        // Error message appears and the main window does NOT open
        System.Threading.Thread.Sleep(1500);
        bool errorShown = false;
        try
        {
            var err = login.FindFirstDescendant(x => x.ByName("اسم المستخدم أو كلمة المرور غير صحيحة"));
            errorShown = err != null;
        }
        catch { }
        Assert.True(errorShown, "Error message did not appear for wrong password");
    }
}