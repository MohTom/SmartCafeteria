using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SmartCafeteria.Licensing.Hardware;

public sealed class WindowsHardwareIdProvider : IHardwareIdProvider
{
    private const string CryptographyKeyPath = @"SOFTWARE\Microsoft\Cryptography";

    public HardwareIdentity GetIdentity()
    {
        var machineGuid = ReadMachineGuid();
        var volumeSerial = ReadVolumeSerial();
        var idBytes = HardwareId.Compute(machineGuid, volumeSerial);
        var macBytes = ReadMacAddress();

        return new HardwareIdentity(
            idBytes,
            HardwareId.Format(idBytes),
            machineGuid,
            HardwareId.FormatMac(macBytes),
            macBytes);
    }

    public static string ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CryptographyKeyPath);
            var value = key?.GetValue("MachineGuid") as string;

            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        catch
        {
        }

        return Environment.MachineName;
    }

    private static uint ReadVolumeSerial()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            if (GetVolumeInformationW(root, null, 0, out var serial, out _, out _, null, 0))
                return serial;
        }
        catch
        {
        }

        return 0;
    }

    private static byte[] ReadMacAddress()
    {
        try
        {
            var candidate = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .Where(network => network.OperationalStatus == OperationalStatus.Up)
                .Where(network => network.GetPhysicalAddress().GetAddressBytes().Length == 6)
                .OrderBy(network => network.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidate?.GetPhysicalAddress().GetAddressBytes() ?? new byte[6];
        }
        catch
        {
            return new byte[6];
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationW(
        string rootPathName,
        char[]? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        char[]? fileSystemNameBuffer,
        int fileSystemNameSize);
}
