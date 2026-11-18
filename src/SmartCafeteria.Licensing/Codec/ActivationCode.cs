using System.Buffers.Binary;
using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.Codec;

public sealed record DecodedActivation(
    LicenseKind Kind,
    DateTimeOffset IssueUtc,
    DateTimeOffset? ExpiresUtc,
    byte[] HardwareId,
    byte[] Nonce,
    byte[] Payload,
    byte[] Signature);

public static class ActivationCode
{
    private const byte Version = 1;
    private const int HardwareLength = 12;
    private const int NonceLength = 4;
    private const int PayloadLength = 27;
    private const int SignatureLength = 64;
    private const int TotalLength = PayloadLength + SignatureLength;

    public static string Encode(
        LicenseKind kind,
        DateTimeOffset issueUtc,
        DateTimeOffset? expiresUtc,
        ReadOnlySpan<byte> hardwareId,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> signature)
    {
        if (kind is not (LicenseKind.Monthly or LicenseKind.Permanent))
            throw new ArgumentException("Unsupported license kind.", nameof(kind));

        if (hardwareId.Length != HardwareLength)
            throw new ArgumentException("Hardware id must be 12 bytes.", nameof(hardwareId));

        if (nonce.Length != NonceLength)
            throw new ArgumentException("Nonce must be 4 bytes.", nameof(nonce));

        if (signature.Length != SignatureLength)
            throw new ArgumentException("Signature must be 64 bytes.", nameof(signature));

        Span<byte> payload = stackalloc byte[PayloadLength];
        payload[0] = Version;
        payload[1] = (byte)kind;
        LicenseDate.Write(issueUtc, payload.Slice(2, 4));
        LicenseDate.WriteNullable(expiresUtc, payload.Slice(6, 4));
        hardwareId.CopyTo(payload.Slice(10, HardwareLength));
        nonce.CopyTo(payload.Slice(22, NonceLength));
        payload[26] = kind == LicenseKind.Permanent ? (byte)1 : (byte)0;

        Span<byte> output = stackalloc byte[TotalLength];
        payload.CopyTo(output);
        signature.CopyTo(output.Slice(PayloadLength));

        return Base32Crockford.Format(Base32Crockford.Encode(output));
    }

    public static bool TryDecode(string value, out DecodedActivation? activation, out string error)
    {
        activation = null;
        error = string.Empty;

        if (!Base32Crockford.TryDecode(value, out var bytes))
        {
            error = "صيغة رمز التفعيل غير صحيحة.";
            return false;
        }

        if (bytes.Length != TotalLength)
        {
            error = "طول رمز التفعيل غير صحيح.";
            return false;
        }

        var payload = bytes.AsSpan(0, PayloadLength).ToArray();
        var signature = bytes.AsSpan(PayloadLength, SignatureLength).ToArray();

        if (payload[0] != Version)
        {
            error = "إصدار رمز التفعيل غير مدعوم.";
            return false;
        }

        var kind = (LicenseKind)payload[1];
        if (kind is not (LicenseKind.Monthly or LicenseKind.Permanent))
        {
            error = "نوع الترخيص غير صحيح.";
            return false;
        }

        var flags = payload[26];
        if ((kind == LicenseKind.Permanent) != (flags == 1))
        {
            error = "بيانات الترخيص غير متسقة.";
            return false;
        }

        var issueUtc = LicenseDate.Read(payload.AsSpan(2, 4));
        var expiresUtc = LicenseDate.ReadNullable(payload.AsSpan(6, 4));

        if (kind == LicenseKind.Monthly && (!expiresUtc.HasValue || expiresUtc <= issueUtc))
        {
            error = "تاريخ انتهاء الترخيص غير صحيح.";
            return false;
        }

        if (kind == LicenseKind.Permanent && expiresUtc.HasValue)
        {
            error = "الترخيص الدائم لا يحتوي تاريخ انتهاء.";
            return false;
        }

        activation = new DecodedActivation(
            kind,
            issueUtc,
            expiresUtc,
            payload.AsSpan(10, HardwareLength).ToArray(),
            payload.AsSpan(22, NonceLength).ToArray(),
            payload,
            signature);

        return true;
    }
}
