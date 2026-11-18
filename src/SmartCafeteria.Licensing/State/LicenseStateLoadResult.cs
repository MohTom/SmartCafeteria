using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.State;

public sealed record LicenseStateLoadResult(
    LicenseState? State,
    bool Tampered,
    string? Error);

public interface ILicenseStateStore
{
    LicenseStateLoadResult Load();

    bool Save(LicenseState state);
}
