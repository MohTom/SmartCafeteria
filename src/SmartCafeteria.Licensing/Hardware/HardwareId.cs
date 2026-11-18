using System.Security.Cryptography;
using System.Text;

namespace SmartCafeteria.Licensing.Hardware;

public static class HardwareId
{
    public static byte[] Compute(string machineGuid, uint volumeSerial)
    {
        var material = $"SCFP1|{machineGuid.Trim().ToUpperInvariant()}|{volumeSerial:X8}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return hash[..12];
    }

    public static string Format(ReadOnlySpan<byte> idBytes)
    {
        var hex = Convert.ToHexString(idBytes);
        var builder = new StringBuilder(hex.Length + hex.Length / 4);

        for (var i = 0; i < hex.Length; i += 4)
        {
            if (i > 0)
                builder.Append('-');

            builder.Append(hex, i, Math.Min(4, hex.Length - i));
        }

        return builder.ToString();
    }

    public static string FormatMac(ReadOnlySpan<byte> macBytes) =>
        macBytes.Length == 0 ? string.Empty : string.Join('-', macBytes.ToArray().Select(value => value.ToString("X2")));
}
