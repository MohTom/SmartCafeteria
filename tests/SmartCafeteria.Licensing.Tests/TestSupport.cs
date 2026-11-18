using System.Security.Cryptography;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;

namespace SmartCafeteria.Licensing.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartCafeteria.Licensing.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
        }
    }
}

internal sealed class FakeHardwareProvider : IHardwareIdProvider
{
    public FakeHardwareProvider(string machineGuid = "machine-guid-1", byte[]? idBytes = null, byte[]? macBytes = null)
    {
        IdBytes = idBytes ?? Convert.FromHexString("0102030405060708090A0B0C");
        MacBytes = macBytes ?? Convert.FromHexString("AABBCCDDEEFF");
        MachineGuid = machineGuid;
    }

    public byte[] IdBytes { get; }

    public byte[] MacBytes { get; }

    public string MachineGuid { get; }

    public HardwareIdentity GetIdentity() => new(
        IdBytes,
        HardwareId.Format(IdBytes),
        MachineGuid,
        HardwareId.FormatMac(MacBytes),
        MacBytes);
}

internal sealed class MutableTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class EcdsaFixture : IDisposable
{
    public EcdsaFixture()
    {
        Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublicKey = Key.ExportSubjectPublicKeyInfo();
    }

    public ECDsa Key { get; }

    public byte[] PublicKey { get; }

    public void Dispose() => Key.Dispose();
}

internal static class TestLog
{
    public static ILicenseLog Create() => NullLicenseLog.Instance;
}
