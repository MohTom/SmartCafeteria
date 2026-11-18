using FlaUI.Core.AutomationElements;
using Xunit;

namespace SmartCafeteria.UI.Tests;

public class ActivationTests : AppTestBase
{
    [Fact]
    public void Settings_ShowsActivationSectionAndDeviceId()
    {
        NavigateTo("الإعدادات");

        Assert.True(HasText("التفعيل"), "Activation section missing");
        Assert.True(HasText("طلب التفعيل"), "Activation request tab missing");

        var deviceId = MainWindow.FindFirstDescendant(x => x.ByAutomationId("LicenseDeviceId"));
        Assert.NotNull(deviceId);
        Assert.Contains("معرف الجهاز", deviceId!.Name);
    }

    [Fact]
    public void Settings_GeneratesActivationRequest()
    {
        NavigateTo("الإعدادات");

        var generate = MainWindow.FindFirstDescendant(x => x.ByAutomationId("GenerateActivationRequestButton"));
        Assert.NotNull(generate);
        SafeInvoke(generate, 1200);

        var requestBox = MainWindow.FindFirstDescendant(x => x.ByAutomationId("ActivationRequestCode"));
        Assert.NotNull(requestBox);
        var textBox = requestBox!.AsTextBox();

        for (var attempt = 0; attempt < 20 && string.IsNullOrWhiteSpace(textBox.Text); attempt++)
            System.Threading.Thread.Sleep(250);

        Assert.False(string.IsNullOrWhiteSpace(textBox.Text), "Activation request code was not generated");
        Assert.True(HasText("تم إنشاء طلب التفعيل. انسخ الرمز وأرسله إلى مزود البرنامج."),
            "Activation request status message missing");
    }

    [Fact]
    public void Settings_InvalidActivationCode_ShowsError()
    {
        NavigateTo("الإعدادات");

        var activationTab = MainWindow.FindFirstDescendant(x => x.ByAutomationId("ActivationEntryTab"));
        Assert.NotNull(activationTab);
        var selection = activationTab!.Patterns.SelectionItem.PatternOrDefault;
        Assert.NotNull(selection);
        selection.Select();
        System.Threading.Thread.Sleep(600);

        EnterById("ActivationCodeInput", "INVALID-CODE");

        var activate = MainWindow.FindFirstDescendant(x => x.ByAutomationId("ActivateLicenseButton"));
        Assert.NotNull(activate);
        SafeInvoke(activate, 800);

        Assert.True(HasText("صيغة رمز التفعيل غير صحيحة."), "Invalid activation error missing");
    }
}
