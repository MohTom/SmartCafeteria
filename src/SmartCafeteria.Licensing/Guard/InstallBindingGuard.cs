using Microsoft.Win32;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Logging;

namespace SmartCafeteria.Licensing.Guard;

public sealed class InstallBindingGuard : IInstallGuard
{
    private const string InstallRegistryPath = @"SOFTWARE\SmartCafeteria";
    private const string InstallRegistryValue = "InstallMachineGuid";

    private readonly IHardwareIdProvider _hardware;
    private readonly ILicenseLog _log;
    private readonly string _markerPath;
    private readonly string _recordPath;
    private readonly Func<string?> _readInstalledMachineGuid;
    private readonly bool _bypassInDebug;

    public InstallBindingGuard(
        IHardwareIdProvider hardware,
        ILicenseLog log,
        string markerPath,
        string recordPath,
        Func<string?>? readInstalledMachineGuid = null,
        bool bypassInDebug = true)
    {
        _hardware = hardware;
        _log = log;
        _markerPath = markerPath;
        _recordPath = recordPath;
        _readInstalledMachineGuid = readInstalledMachineGuid ?? ReadInstalledMachineGuid;
        _bypassInDebug = bypassInDebug;
    }

    public static InstallBindingGuard CreateDefault(IHardwareIdProvider hardware, ILicenseLog log) =>
        new(
            hardware,
            log,
            Path.Combine(AppContext.BaseDirectory, "install.marker"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SmartCafeteria",
                "Licensing",
                "install.id"));

    public InstallGuardResult Check()
    {
#if DEBUG
        if (_bypassInDebug ||
            string.Equals(
                Environment.GetEnvironmentVariable("SMART_CAFETERIA_SKIP_INSTALL_GUARD"),
                "1",
                StringComparison.Ordinal))
        {
            return InstallGuardResult.Allowed();
        }
#endif

        if (!File.Exists(_markerPath))
            return InstallGuardResult.Blocked("لم يتم العثور على ملف التثبيت. أعد تثبيت البرنامج.");

        var installedMachineGuid = _readInstalledMachineGuid();
        if (string.IsNullOrWhiteSpace(installedMachineGuid))
            return InstallGuardResult.Blocked("تسجيل التثبيت غير موجود. أعد تثبيت البرنامج.");

        var hardware = _hardware.GetIdentity();
        if (!string.Equals(installedMachineGuid.Trim(), hardware.MachineGuid.Trim(), StringComparison.OrdinalIgnoreCase))
            return InstallGuardResult.Blocked("هذا النسخ غير مثبت على هذا الجهاز.");

        try
        {
            if (File.Exists(_recordPath))
            {
                var recorded = File.ReadAllText(_recordPath).Trim();
                if (!string.Equals(recorded, hardware.DeviceId, StringComparison.OrdinalIgnoreCase))
                    return InstallGuardResult.Blocked("بيانات التثبيت لا تطابق هذا الجهاز.");
            }
            else
            {
                var directory = Path.GetDirectoryName(_recordPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_recordPath, hardware.DeviceId);
            }
        }
        catch (Exception exception)
        {
            _log.Write($"Install binding record check failed: {exception.Message}");
        }

        return InstallGuardResult.Allowed();
    }

    private static string? ReadInstalledMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(InstallRegistryPath);
            return key?.GetValue(InstallRegistryValue) as string;
        }
        catch
        {
            return null;
        }
    }
}
