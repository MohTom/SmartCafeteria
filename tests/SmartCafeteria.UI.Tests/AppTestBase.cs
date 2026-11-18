using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>
/// Base class that launches the app, logs in with the default admin account,
/// and exposes the resulting MainWindow for feature tests.
/// </summary>
public abstract class AppTestBase : IDisposable
{
    protected readonly string ExePath;
    protected readonly Application App;
    protected readonly UIA3Automation Automation;
    protected readonly UiTestDatabase TestData;
    protected Window MainWindow;

    protected AppTestBase(
        Action<UiTestDatabase>? prepare = null,
        string loginUsername = UiTestDatabase.AdminUsername,
        string loginPassword = UiTestDatabase.AdminPassword)
    {
        var baseDir = AppContext.BaseDirectory;
        var exes = Directory.GetFiles(baseDir, "SmartCafeteria.exe", SearchOption.AllDirectories);
        Assert.NotEmpty(exes);
        ExePath = exes[0];

        TestData = UiTestDatabase.Create();
        prepare?.Invoke(TestData);

        Automation = new UIA3Automation();
        App = TestAppProcessGuard.Instance.Launch(ExePath);
        MainWindow = LoginAndGetMainWindow(loginUsername, loginPassword);
        Assert.NotNull(MainWindow);
    }

    public void Dispose()
    {
        TestAppProcessGuard.Instance.Release(App);
        Automation?.Dispose();
        TestData?.Dispose();
    }

    protected Window LoginAndGetMainWindow(string username, string password)
    {
        Window? mainWindow = null;
        for (var attempt = 0; attempt < 2 && mainWindow == null; attempt++)
        {
            var loginWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(30));
            Assert.NotNull(loginWindow);

            AutomationElement? usernameBox = null;
            for (var i = 0; i < 40 && usernameBox is null; i++)
            {
                try { usernameBox = loginWindow.FindFirstDescendant(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)); }
                catch { }
                if (usernameBox is null) System.Threading.Thread.Sleep(250);
            }
            Assert.NotNull(usernameBox);
            usernameBox!.AsTextBox().Enter(username);

            AutomationElement? passwordBox = null;
            for (var i = 0; i < 40 && passwordBox is null; i++)
            {
                try { passwordBox = loginWindow.FindFirstDescendant(x => x.ByAutomationId("PasswordInput")); }
                catch { }
                if (passwordBox is null) System.Threading.Thread.Sleep(250);
            }
            Assert.NotNull(passwordBox);
            passwordBox!.AsTextBox().Enter(password);

            System.Threading.Thread.Sleep(250);
            var loginButton = loginWindow.FindFirstDescendant(x => x.ByName("دخول"));
            Assert.NotNull(loginButton);
            var invoke = loginButton.Patterns.Invoke.PatternOrDefault;
            if (invoke != null) invoke.Invoke();
            else loginButton.Click();

            for (int i = 0; i < 30; i++)
            {
                System.Threading.Thread.Sleep(500);
                try
                {
                    var w = App.GetMainWindow(Automation, TimeSpan.FromSeconds(3));
                    if (w != null && !string.IsNullOrEmpty(w.Name) && w.Name != loginWindow.Name)
                    {
                        mainWindow = w;
                        break;
                    }
                }
                catch { }
            }
        }

        Assert.NotNull(mainWindow);
        return mainWindow!;
    }

    /// <summary>Click a navigation RadioButton in the sidebar by its label and wait for the view to render.</summary>
    protected AutomationElement? NavigateTo(string navLabel)
    {
        AutomationElement? target = null;
        try { target = MainWindow.FindFirstDescendant(x => x.ByName(navLabel)); }
        catch { }

        if (target == null)
        {
            // Fallback: search radio buttons
            try
            {
                foreach (var rb in MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.RadioButton)))
                {
                    if (!string.IsNullOrEmpty(rb.Name) && rb.Name.Contains(navLabel))
                    {
                        target = rb;
                        break;
                    }
                }
            }
            catch { }
        }

        Assert.True(target != null, $"Navigation target '{navLabel}' not found");
        target!.Click();
        System.Threading.Thread.Sleep(800);
        return target;
    }

    /// <summary>True if the main window contains an element with the given text, polling up to ~4s
    /// to allow asynchronous view loading (products/orders/inventory/settings bind async).</summary>
    protected bool HasText(string text)
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                if (MainWindow.FindFirstDescendant(x => x.ByName(text)) != null)
                    return true;
            }
            catch { }
            System.Threading.Thread.Sleep(500);
        }
        return false;
    }

    /// <summary>True if the main window contains any element with Arabic text, polling up to ~4s.</summary>
    protected bool HasAnyArabicText()
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                foreach (var el in MainWindow.FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Text)))
                {
                    var name = el.Name;
                    if (!string.IsNullOrEmpty(name) && name.ToCharArray().Any(c => c >= 0x0600 && c <= 0x06FF))
                        return true;
                }
            }
            catch { }
            System.Threading.Thread.Sleep(500);
        }
        return false;
    }

    /// <summary>Find a descendant button/control by its displayed text.</summary>
    protected AutomationElement? FindByName(string name)
    {
        try
        {
            return MainWindow.FindFirstDescendant(x => x.ByName(name));
        }
        catch { }
        return null;
    }

    /// <summary>Invoke (or click) an element; scroll-safe for off-screen buttons.</summary>
    protected void SafeInvoke(AutomationElement? el, int waitMs = 700)
    {
        if (el == null) return;
        var inv = el.Patterns.Invoke.PatternOrDefault;
        if (inv != null) inv.Invoke();
        else el.Click();
        System.Threading.Thread.Sleep(waitMs);
    }

    /// <summary>Enter text into an element by AutomationId.</summary>
    protected void EnterById(string automationId, string text)
    {
        var box = MainWindow.FindFirstDescendant(x => x.ByAutomationId(automationId));
        Assert.NotNull(box);
        box.AsTextBox().Enter(text);
    }
}