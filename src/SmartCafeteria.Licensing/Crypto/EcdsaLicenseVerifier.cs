using System.Security.Cryptography;
using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.Service;

namespace SmartCafeteria.Licensing.Crypto;

public sealed class EcdsaLicenseVerifier
{
    private readonly IReadOnlyList<byte[]> _publicKeys;

    public EcdsaLicenseVerifier()
        : this(LicenseKeys.GetVerificationKeys())
    {
    }

    public EcdsaLicenseVerifier(IReadOnlyList<byte[]> publicKeys)
    {
        _publicKeys = publicKeys;
    }

    public ActivationVerification Verify(string code, HardwareIdentity hardware, DateTimeOffset now)
    {
        if (!ActivationCode.TryDecode(code, out var activation, out var error))
            return ActivationVerification.Invalid(error);

        if (activation is null)
            return ActivationVerification.Invalid("رمز التفعيل غير صالح.");

        if (!IsSignatureValid(activation))
            return ActivationVerification.Invalid("توقيع رمز التفعيل غير صالح.");

        if (!CryptographicOperations.FixedTimeEquals(activation.HardwareId, hardware.IdBytes))
            return ActivationVerification.Invalid("رمز التفعيل مخصص لجهاز آخر.");

        if (activation.IssueUtc > now.AddDays(1))
            return ActivationVerification.Invalid("تاريخ إصدار الترخيص في المستقبل.");

        if (activation.Kind == LicenseKind.Monthly &&
            (!activation.ExpiresUtc.HasValue || LicensePolicy.EndOfUtcDay(activation.ExpiresUtc.Value) <= now))
            return ActivationVerification.Invalid("انتهت صلاحية رمز التفعيل.");

        return new ActivationVerification(
            true,
            "رمز التفعيل صالح.",
            activation.Kind,
            activation.IssueUtc,
            activation.ExpiresUtc,
            activation.HardwareId,
            activation.Nonce);
    }

    private bool IsSignatureValid(DecodedActivation activation)
    {
        foreach (var publicKey in _publicKeys)
        {
            try
            {
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(publicKey, out _);

                if (key.VerifyData(
                        activation.Payload,
                        activation.Signature,
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                {
                    return true;
                }
            }
            catch (CryptographicException)
            {
            }
        }

        return false;
    }
}
