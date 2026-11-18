namespace SmartCafeteria.Licensing.Models;

public sealed record ActivationResult(bool Success, string Message, LicenseStatus? Status)
{
    public static ActivationResult Fail(string message) => new(false, message, null);
}

public sealed record ActivationVerification(
    bool IsValid,
    string Message,
    LicenseKind Kind,
    DateTimeOffset IssueUtc,
    DateTimeOffset? ExpiresUtc,
    byte[] HardwareId,
    byte[] Nonce)
{
    public static ActivationVerification Invalid(string message) =>
        new(false, message, LicenseKind.None, default, null, Array.Empty<byte>(), Array.Empty<byte>());
}
