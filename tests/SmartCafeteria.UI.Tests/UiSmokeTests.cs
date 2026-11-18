using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Xunit;

namespace SmartCafeteria.UI.Tests;

public class UiSmokeTests : IDisposable
{
    private readonly UiTestDatabase _testData = UiTestDatabase.Create();

    public void Dispose() => _testData.Dispose();

    private static string GetAppExePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var exeCandidates = Directory.GetFiles(baseDir, "SmartCafeteria.exe", SearchOption.AllDirectories);
        if (exeCandidates.Length > 0)
            return exeCandidates[0];

        var src = Path.Combine(baseDir.Split("tests")[0], "src");
        var appDir = Path.Combine(src, "SmartCafeteria");
        var allExes = Directory.GetFiles(appDir, "SmartCafeteria.exe", SearchOption.AllDirectories);
        Assert.True(allExes.Length > 0, "SmartCafeteria.exe was not found in build output");
        return allExes[0];
    }

    [Fact]
    public void AppExe_Exists()
    {
        var exePath = GetAppExePath();
        Assert.True(File.Exists(exePath), $"App exe not found at {exePath}");
        Assert.True(new FileInfo(exePath).Length > 0);
    }

    [Fact]
    public void LoginWindow_Shown_On_Start()
    {
        var exePath = GetAppExePath();

        var app = TestAppProcessGuard.Instance.Launch(exePath);
        var automation = new UIA3Automation();
        try
        {
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20));
            Assert.NotNull(window);

            // A login window should show a password field
            var passwordInput = window.FindFirstDescendant(x => x.ByAutomationId("PasswordInput"));
            Assert.NotNull(passwordInput);
        }
        finally
        {
            TestAppProcessGuard.Instance.Release(app);
            automation.Dispose();
        }
    }
}