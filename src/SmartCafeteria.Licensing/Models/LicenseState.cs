namespace SmartCafeteria.Licensing.Models;

public sealed record LicenseState(
    DateTimeOffset FirstRunUtc,
    DateTimeOffset LastSeenUtc,
    LicenseKind Kind,
    DateTimeOffset? ExpiresUtc,
    string DeviceId);
