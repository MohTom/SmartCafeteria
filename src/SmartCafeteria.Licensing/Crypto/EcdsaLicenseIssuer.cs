using System.Security.Cryptography;
using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.Crypto;

public sealed class EcdsaLicenseIssuer
{
    private readonly ECDsa _privateKey;

    public EcdsaLicenseIssuer(ECDsa privateKey)
    {
        _privateKey = privateKey;
    }

    public string Issue(
        LicenseKind kind,
        DateTimeOffset issueUtc,
        DateTimeOffset? expiresUtc,
        ReadOnlySpan<byte> hardwareId,
        ReadOnlySpan<byte> nonce)
    {
        if (kind == LicenseKind.Monthly && (!expiresUtc.HasValue || expiresUtc <= issueUtc))
            throw new ArgumentException("Monthly licenses require a future expiry.", nameof(expiresUtc));

        if (kind == LicenseKind.Permanent && expiresUtc.HasValue)
            throw new ArgumentException("Permanent licenses cannot expire.", nameof(expiresUtc));

        Span<byte> payload = stackalloc byte[27];
        payload[0] = 1;
        payload[1] = (byte)kind;
        LicenseDate.Write(issueUtc, payload.Slice(2, 4));
        LicenseDate.WriteNullable(expiresUtc, payload.Slice(6, 4));
        hardwareId.CopyTo(payload.Slice(10, 12));
        nonce.CopyTo(payload.Slice(22, 4));
        payload[26] = kind == LicenseKind.Permanent ? (byte)1 : (byte)0;

        var signature = _privateKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return ActivationCode.Encode(kind, issueUtc, expiresUtc, hardwareId, nonce, signature);
    }
}
