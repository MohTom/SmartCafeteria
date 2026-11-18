using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Models;
using Xunit;

namespace SmartCafeteria.Licensing.Tests;

public sealed class CryptoTests
{
    private static readonly DateTimeOffset Issue = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MonthlyCode_Verifies_ForMatchingHardware()
    {
        using var fixture = new EcdsaFixture();
        var hardware = new FakeHardwareProvider().GetIdentity();
        var issuer = new EcdsaLicenseIssuer(fixture.Key);
        var nonce = new byte[] { 1, 2, 3, 4 };
        var expiry = Issue.AddMonths(1);

        var code = issuer.Issue(LicenseKind.Monthly, Issue, expiry, hardware.IdBytes, nonce);
        var verifier = new EcdsaLicenseVerifier(new[] { fixture.PublicKey });
        var result = verifier.Verify(code, hardware, Issue.AddDays(5));

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(LicenseKind.Monthly, result.Kind);
        Assert.Equal(expiry, result.ExpiresUtc);
        Assert.Equal(nonce, result.Nonce);
    }

    [Fact]
    public void Code_RejectsDifferentHardware()
    {
        using var fixture = new EcdsaFixture();
        var hardware = new FakeHardwareProvider().GetIdentity();
        var other = new FakeHardwareProvider(idBytes: Convert.FromHexString("0C0B0A090807060504030201")).GetIdentity();
        var issuer = new EcdsaLicenseIssuer(fixture.Key);

        var code = issuer.Issue(
            LicenseKind.Permanent,
            Issue,
            null,
            hardware.IdBytes,
            new byte[] { 1, 2, 3, 4 });

        var verifier = new EcdsaLicenseVerifier(new[] { fixture.PublicKey });
        var result = verifier.Verify(code, other, Issue);

        Assert.False(result.IsValid);
        Assert.Contains("جهاز آخر", result.Message);
    }

    [Fact]
    public void Code_RejectsExpiredMonthlyLicense()
    {
        using var fixture = new EcdsaFixture();
        var hardware = new FakeHardwareProvider().GetIdentity();
        var issuer = new EcdsaLicenseIssuer(fixture.Key);

        var code = issuer.Issue(
            LicenseKind.Monthly,
            Issue,
            Issue.AddDays(30),
            hardware.IdBytes,
            new byte[] { 1, 2, 3, 4 });

        var verifier = new EcdsaLicenseVerifier(new[] { fixture.PublicKey });
        var result = verifier.Verify(code, hardware, Issue.AddDays(31));

        Assert.False(result.IsValid);
        Assert.Contains("انتهت", result.Message);
    }

    [Fact]
    public void Code_RejectsModifiedSignature()
    {
        using var fixture = new EcdsaFixture();
        var hardware = new FakeHardwareProvider().GetIdentity();
        var issuer = new EcdsaLicenseIssuer(fixture.Key);

        var code = issuer.Issue(
            LicenseKind.Monthly,
            Issue,
            Issue.AddMonths(1),
            hardware.IdBytes,
            new byte[] { 1, 2, 3, 4 });

        Assert.True(ActivationCode.TryDecode(code, out var decoded, out var error), error);
        var signature = decoded!.Signature.ToArray();
        signature[0] ^= 0xFF;
        var tampered = ActivationCode.Encode(
            decoded.Kind,
            decoded.IssueUtc,
            decoded.ExpiresUtc,
            decoded.HardwareId,
            decoded.Nonce,
            signature);

        var verifier = new EcdsaLicenseVerifier(new[] { fixture.PublicKey });
        var result = verifier.Verify(tampered, hardware, Issue);

        Assert.False(result.IsValid);
        Assert.Contains("توقيع", result.Message);
    }

    [Fact]
    public void PublicKey_CanBeImported_FromEmbeddedConstant()
    {
        var keys = LicenseKeys.GetVerificationKeys();

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key, out _);
        }
    }
}
