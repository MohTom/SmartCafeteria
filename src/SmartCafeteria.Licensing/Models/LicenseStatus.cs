namespace SmartCafeteria.Licensing.Models;

public sealed record LicenseStatus(
    LicenseKind Kind,
    bool IsTrial,
    bool IsLocked,
    LockReason LockReason,
    DateTimeOffset? ExpiresUtc,
    int TrialDaysRemaining,
    string DeviceId,
    string Message)
{
    public bool IsPermanent => Kind == LicenseKind.Permanent;

    public bool IsActivated => Kind is LicenseKind.Monthly or LicenseKind.Permanent;
}
