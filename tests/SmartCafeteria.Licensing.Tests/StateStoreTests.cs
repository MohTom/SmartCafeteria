using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.State;
using Xunit;

namespace SmartCafeteria.Licensing.Tests;

public sealed class StateStoreTests
{
    [Fact]
    public void State_RoundTrips_WhenEncrypted()
    {
        using var directory = new TempDirectory();
        var hardware = new FakeHardwareProvider();
        var path = directory.File("license.dat");
        var store = new EncryptedLicenseStateStore(hardware, TestLog.Create(), new[] { path }, includeRegistry: false);
        var firstRun = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var state = new LicenseState(
            firstRun,
            firstRun.AddDays(3),
            LicenseKind.Monthly,
            firstRun.AddMonths(1),
            hardware.GetIdentity().DeviceId);

        Assert.True(store.Save(state));
        var loaded = store.Load();

        Assert.False(loaded.Tampered);
        Assert.NotNull(loaded.State);
        Assert.Equal(state, loaded.State);
        Assert.DoesNotContain("SCFP1", File.ReadAllText(path));
    }

    [Fact]
    public void State_ReportsTamper_WhenFileIsModified()
    {
        using var directory = new TempDirectory();
        var hardware = new FakeHardwareProvider();
        var path = directory.File("license.dat");
        var store = new EncryptedLicenseStateStore(hardware, TestLog.Create(), new[] { path }, includeRegistry: false);
        var hardwareIdentity = hardware.GetIdentity();

        Assert.True(store.Save(new LicenseState(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            LicenseKind.None,
            null,
            hardwareIdentity.DeviceId)));

        File.WriteAllText(path, "tampered");
        var loaded = store.Load();

        Assert.True(loaded.Tampered);
        Assert.Null(loaded.State);
    }

    [Fact]
    public void State_ReportsTamper_WhenOpenedOnAnotherMachine()
    {
        using var directory = new TempDirectory();
        var path = directory.File("license.dat");
        var original = new FakeHardwareProvider("machine-a");
        var storeA = new EncryptedLicenseStateStore(original, TestLog.Create(), new[] { path }, includeRegistry: false);

        Assert.True(storeA.Save(new LicenseState(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            LicenseKind.None,
            null,
            original.GetIdentity().DeviceId)));

        var other = new FakeHardwareProvider("machine-b");
        var storeB = new EncryptedLicenseStateStore(other, TestLog.Create(), new[] { path }, includeRegistry: false);
        var loaded = storeB.Load();

        Assert.True(loaded.Tampered);
        Assert.Null(loaded.State);
    }

    [Fact]
    public void State_ReturnsNull_WhenNoAnchorsExist()
    {
        using var directory = new TempDirectory();
        var store = new EncryptedLicenseStateStore(
            new FakeHardwareProvider(),
            TestLog.Create(),
            new[] { directory.File("missing.dat") },
            includeRegistry: false);

        var loaded = store.Load();

        Assert.False(loaded.Tampered);
        Assert.Null(loaded.State);
        Assert.Null(loaded.Error);
    }
}
