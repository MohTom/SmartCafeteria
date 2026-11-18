using SmartCafeteria.Licensing.Guard;
using Xunit;

namespace SmartCafeteria.Licensing.Tests;

public sealed class InstallGuardTests
{
    [Fact]
    public void MissingMarker_BlocksInstallation()
    {
        using var directory = new TempDirectory();
        var guard = CreateGuard(directory, "machine-a", "machine-a");

        var result = guard.Check();

        Assert.False(result.IsAllowed);
        Assert.Contains("ملف التثبيت", result.Message);
    }

    [Fact]
    public void MatchingMarkerAndRegistry_AllowsAndWritesRecord()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("install.marker"), "marker");
        var guard = CreateGuard(directory, "machine-a", "machine-a");

        var result = guard.Check();

        Assert.True(result.IsAllowed);
        Assert.True(File.Exists(directory.File("install.id")));
        Assert.Equal("0102-0304-0506-0708-090A-0B0C", File.ReadAllText(directory.File("install.id")));
    }

    [Fact]
    public void RegistryMachineMismatch_BlocksInstallation()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("install.marker"), "marker");
        var guard = CreateGuard(directory, "machine-a", "machine-b");

        var result = guard.Check();

        Assert.False(result.IsAllowed);
        Assert.Contains("هذا الجهاز", result.Message);
    }

    [Fact]
    public void MissingRegistry_BlocksInstallation()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("install.marker"), "marker");
        var guard = CreateGuard(directory, "machine-a", null);

        var result = guard.Check();

        Assert.False(result.IsAllowed);
        Assert.Contains("تسجيل التثبيت", result.Message);
    }

    [Fact]
    public void ExistingDeviceRecordMismatch_BlocksInstallation()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.File("install.marker"), "marker");
        File.WriteAllText(directory.File("install.id"), "another-device");
        var guard = CreateGuard(directory, "machine-a", "machine-a");

        var result = guard.Check();

        Assert.False(result.IsAllowed);
        Assert.Contains("لا تطابق", result.Message);
    }

    private static InstallBindingGuard CreateGuard(TempDirectory directory, string hardwareGuid, string? registryGuid) =>
        new(
            new FakeHardwareProvider(hardwareGuid),
            TestLog.Create(),
            directory.File("install.marker"),
            directory.File("install.id"),
            () => registryGuid,
            bypassInDebug: false);
}
