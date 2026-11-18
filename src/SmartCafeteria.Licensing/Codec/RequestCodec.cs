using System.Buffers.Binary;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.Codec;

public static class RequestCodec
{
    private const byte Version = 1;
    private const int HardwareLength = 12;
    private const int MacLength = 6;
    private const int NonceLength = 4;
    private const int PayloadLength = 32;
    private const int TotalLength = PayloadLength + 4;

    public static string Encode(ActivationRequestInfo request)
    {
        if (request.Kind is not (LicenseKind.Monthly or LicenseKind.Permanent))
            throw new ArgumentException("Unsupported license kind.", nameof(request));

        if (request.HardwareId.Length != HardwareLength)
            throw new ArgumentException("Hardware id must be 12 bytes.", nameof(request));

        if (request.MacBytes.Length != MacLength)
            throw new ArgumentException("MAC address must be 6 bytes.", nameof(request));

        if (request.Nonce.Length != NonceLength)
            throw new ArgumentException("Nonce must be 4 bytes.", nameof(request));

        Span<byte> payload = stackalloc byte[TotalLength];
        payload[0] = Version;
        payload[1] = (byte)request.Kind;
        LicenseDate.Write(request.RequestedUtc, payload.Slice(2, 4));
        request.HardwareId.CopyTo(payload.Slice(6, HardwareLength));
        request.MacBytes.CopyTo(payload.Slice(18, MacLength));
        LicenseDate.WriteNullable(request.CurrentExpiryUtc, payload.Slice(24, 4));
        request.Nonce.CopyTo(payload.Slice(28, NonceLength));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.Slice(PayloadLength, 4), Crc32.Compute(payload[..PayloadLength]));

        return Base32Crockford.Format(Base32Crockford.Encode(payload));
    }

    public static bool TryDecode(string code, out ActivationRequestInfo? request, out string error)
    {
        request = null;
        error = string.Empty;

        if (!Base32Crockford.TryDecode(code, out var bytes))
        {
            error = "صيغة طلب التفعيل غير صحيحة.";
            return false;
        }

        if (bytes.Length != TotalLength)
        {
            error = "طول طلب التفعيل غير صحيح.";
            return false;
        }

        var payload = bytes.AsSpan(0, PayloadLength);
        var expected = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(PayloadLength, 4));

        if (Crc32.Compute(payload) != expected)
        {
            error = "طلب التفعيل تالف أو تم تعديله.";
            return false;
        }

        if (payload[0] != Version)
        {
            error = "إصدار طلب التفعيل غير مدعوم.";
            return false;
        }

        var kind = (LicenseKind)payload[1];
        if (kind is not (LicenseKind.Monthly or LicenseKind.Permanent))
        {
            error = "نوع الترخيص غير صحيح.";
            return false;
        }

        var requestedUtc = LicenseDate.Read(payload.Slice(2, 4));
        var hardwareId = payload.Slice(6, HardwareLength).ToArray();
        var macBytes = payload.Slice(18, MacLength).ToArray();
        var currentExpiry = LicenseDate.ReadNullable(payload.Slice(24, 4));
        var nonce = payload.Slice(28, NonceLength).ToArray();

        request = new ActivationRequestInfo(
            kind,
            requestedUtc,
            HardwareId.Format(hardwareId),
            HardwareId.FormatMac(macBytes),
            currentExpiry,
            nonce,
            hardwareId,
            macBytes);

        return true;
    }
}
