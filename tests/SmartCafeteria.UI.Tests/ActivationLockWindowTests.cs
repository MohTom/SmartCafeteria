using System.IO;
using System.Security.Cryptography;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.State;
using Xunit;

namespace SmartCafeteria.UI.Tests;

public sealed class ActivationLockWindowTests
{
    [Fact]
    public void LockedApp_Activation_ClosesWindowOnceAndShowsLogin()
    {
        var previousPublicKey = Environment.GetEnvironmentVariable("SMART_CAFETERIA_LICENSE_PUBLIC_KEY");
        UiTestDatabase? testData = null;
        Application? app = null;
        UIA3Automation? automation = null;

        try
        {
            testData = UiTestDatabase.Create();
            var hardwareProvider = new WindowsHardwareIdProvider();
            var hardware = hardwareProvider.GetIdentity();

            var store = new EncryptedLicenseStateStore(
                hardwareProvider,
                new FileLicenseLog(Path.Combine(testData.DirectoryPath, "license-test.log")),
                new[] { Path.Combine(testData.LicenseDirectoryPath, "license.dat") },
                includeRegistry: false);

            var expiredTrial = new LicenseState(
                DateTimeOffset.UtcNow.AddDays(-30),
                DateTimeOffset.UtcNow.AddDays(-30),
                LicenseKind.None,
                null,
                hardware.DeviceId);

            Assert.True(store.Save(expiredTrial));

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Environment.SetEnvironmentVariable(
                "SMART_CAFETERIA_LICENSE_PUBLIC_KEY",
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));

            var issueUtc = DateTimeOffset.UtcNow;
            var activationCode = new EcdsaLicenseIssuer(key).Issue(
                LicenseKind.Monthly,
                issueUtc,
                issueUtc.AddMonths(1),
                hardware.IdBytes,
                new byte[] { 1, 2, 3, 4 });

            Assert.True(
                ActivationCode.TryDecode(activationCode, out _, out var decodeError),
                $"Generated activation code is invalid: {decodeError}");

            var exePath = Directory.GetFiles(
                AppContext.BaseDirectory,
                "SmartCafeteria.exe",
                SearchOption.AllDirectories)[0];

            automation = new UIA3Automation();
            app = TestAppProcessGuard.Instance.Launch(exePath);

            var lockWindow = WaitForWindow(automation, app, "ActivationEntryTab");
            Assert.NotNull(lockWindow);

            var tab = lockWindow!.FindFirstDescendant(x => x.ByAutomationId("ActivationEntryTab"));
            Assert.NotNull(tab);
            var selection = tab!.Patterns.SelectionItem.PatternOrDefault;
            Assert.NotNull(selection);
            selection.Select();
            Thread.Sleep(250);

            var input = lockWindow.FindFirstDescendant(x => x.ByAutomationId("ActivationCodeInput"));
            Assert.NotNull(input);
            input!.AsTextBox().Text = activationCode;

            var expectedText = Base32Crockford.Normalize(activationCode);
            var actualText = Base32Crockford.Normalize(input.AsTextBox().Text);
            Assert.True(
                actualText == expectedText,
                $"Activation input mismatch. Expected {expectedText.Length} chars, got {actualText.Length}. "
                + $"Actual starts '{actualText[..Math.Min(12, actualText.Length)]}' and ends '{actualText[Math.Max(0, actualText.Length - 12)..]}'.");

            var button = lockWindow.FindFirstDescendant(x => x.ByAutomationId("ActivateLicenseButton"));
            Assert.NotNull(button);
            var invoke = button!.Patterns.Invoke.PatternOrDefault;
            if (invoke != null)
                invoke.Invoke();
            else
                button.Click();

            var loginWindow = WaitForWindow(automation, app, "PasswordInput", 30);
            if (loginWindow is null)
            {
                var current = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                var texts = current?
                    .FindAllDescendants(x => x.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
                    .Select(x => x.Name)
                    .Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>();

                Assert.Fail(
                    $"Login window not found after activation. Current window: '{current?.Name}'. Text: {string.Join(" | ", texts)}");
            }
        }
        finally
        {
            TestAppProcessGuard.Instance.Release(app);
            automation?.Dispose();
            testData?.Dispose();
            Environment.SetEnvironmentVariable("SMART_CAFETERIA_LICENSE_PUBLIC_KEY", previousPublicKey);
        }
    }

    private static Window? WaitForWindow(
        UIA3Automation automation,
        Application app,
        string automationId,
        int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                if (window?.FindFirstDescendant(x => x.ByAutomationId(automationId)) != null)
                    return window;
            }
            catch
            {
            }

            Thread.Sleep(250);
        }

        return null;
    }
}
