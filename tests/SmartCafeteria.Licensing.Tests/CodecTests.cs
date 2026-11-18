using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Models;
using Xunit;

namespace SmartCafeteria.Licensing.Tests;

public sealed class CodecTests
{
    [Fact]
    public void Base32_RoundTrips_And_NormalizesConfusingCharacters()
    {
        var input = Convert.FromHexString("00FF102030405060708090A0B0C0D0E0");
        var encoded = Base32Crockford.Encode(input);

        Assert.True(Base32Crockford.TryDecode(Base32Crockford.Format(encoded), out var decoded));
        Assert.Equal(input, decoded);

        Assert.True(Base32Crockford.TryDecode(encoded.ToLowerInvariant().Replace('0', 'o').Replace('1', 'l'), out var normalized));
        Assert.Equal(input, normalized);
    }

    [Fact]
    public void RequestCodec_RoundTrips()
    {
        var request = new ActivationRequestInfo(
            LicenseKind.Monthly,
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            "0102-0304-0506-0708-090A-0B0C",
            "AA-BB-CC-DD-EE-FF",
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new byte[] { 1, 2, 3, 4 },
            Convert.FromHexString("0102030405060708090A0B0C"),
            Convert.FromHexString("AABBCCDDEEFF"));

        var code = RequestCodec.Encode(request);

        Assert.True(RequestCodec.TryDecode(code, out var decoded, out var error), error);
        Assert.NotNull(decoded);
        Assert.Equal(LicenseKind.Monthly, decoded!.Kind);
        Assert.Equal(request.DeviceId, decoded.DeviceId);
        Assert.Equal(request.RequestedUtc, decoded.RequestedUtc);
        Assert.Equal(request.CurrentExpiryUtc, decoded.CurrentExpiryUtc);
        Assert.Equal(request.Nonce, decoded.Nonce);
    }

    [Fact]
    public void RequestCodec_RejectsTamperedCode()
    {
        var request = new ActivationRequestInfo(
            LicenseKind.Permanent,
            DateTimeOffset.UtcNow,
            "ABCD-1234",
            "AA-BB-CC-DD-EE-FF",
            null,
            new byte[] { 1, 2, 3, 4 },
            Convert.FromHexString("0102030405060708090A0B0C"),
            Convert.FromHexString("AABBCCDDEEFF"));

        var code = RequestCodec.Encode(request).Replace("-", string.Empty);
        var tampered = (code[0] == '0' ? '1' : '0') + code[1..];

        Assert.False(RequestCodec.TryDecode(tampered, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
