using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;

namespace SmartCafeteria.Licensing.State;

public static class LicenseStateStoreFactory
{
    public static ILicenseStateStore CreateDefault(IHardwareIdProvider hardware, ILicenseLog log)
    {
#if DEBUG
        var overrideDirectory = Environment.GetEnvironmentVariable("SMART_CAFETERIA_LICENSE_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return new EncryptedLicenseStateStore(
                hardware,
                log,
                new[] { Path.Combine(overrideDirectory, "license.dat") },
                includeRegistry: false);
        }
#endif

        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SmartCafeteria",
            "Licensing",
            "license.dat");
        var localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartCafeteria",
            "Licensing",
            "license.dat");

        return new EncryptedLicenseStateStore(
            hardware,
            log,
            new[] { programData, localData },
            includeRegistry: true);
    }
}
