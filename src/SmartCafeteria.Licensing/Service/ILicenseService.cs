using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.Service;

public interface ILicenseService
{
    string DeviceId { get; }

    Task<LicenseStatus> GetStatusAsync();

    Task<ActivationRequest> CreateRequestAsync(LicenseKind kind);

    Task<ActivationResult> ActivateAsync(string code);
}
