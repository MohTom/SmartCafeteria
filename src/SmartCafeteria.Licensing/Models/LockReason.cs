namespace SmartCafeteria.Licensing.Models;

public enum LockReason
{
    None = 0,
    TrialExpired = 1,
    LicenseExpired = 2,
    StateCorrupt = 3,
    StateTampered = 4,
    HardwareMismatch = 5,
    InstallInvalid = 6
}
