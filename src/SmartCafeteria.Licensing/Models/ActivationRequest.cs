namespace SmartCafeteria.Licensing.Models;

public sealed record ActivationRequest(
    LicenseKind Kind,
    DateTimeOffset RequestedUtc,
    string DeviceId,
    string MacAddress,
    DateTimeOffset? CurrentExpiryUtc,
    byte[] Nonce,
    string Code);

public sealed record ActivationRequestInfo(
    LicenseKind Kind,
    DateTimeOffset RequestedUtc,
    string DeviceId,
    string MacAddress,
    DateTimeOffset? CurrentExpiryUtc,
    byte[] Nonce,
    byte[] HardwareId,
    byte[] MacBytes);
