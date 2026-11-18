using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.Service;

namespace SmartCafeteria.ViewModels;

public partial class ActivationViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;

    [ObservableProperty] private string _licenseStatusText = "جارٍ قراءة حالة الترخيص...";
    [ObservableProperty] private string _deviceId = string.Empty;
    [ObservableProperty] private string _expiryText = string.Empty;
    [ObservableProperty] private string _lockMessage = string.Empty;
    [ObservableProperty] private bool _isTrial;
    [ObservableProperty] private bool _isActivated;
    [ObservableProperty] private bool _isPermanent;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private int _trialDaysRemaining;
    [ObservableProperty] private bool _canRequestActivation = true;
    [ObservableProperty] private LicenseKindOption? _selectedLicenseKind;
    [ObservableProperty] private string _requestCode = string.Empty;
    [ObservableProperty] private string _requestMessage = string.Empty;
    [ObservableProperty] private string _activationCode = string.Empty;
    [ObservableProperty] private string _activationMessage = string.Empty;

    public ActivationViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        LicenseKinds = new ObservableCollection<LicenseKindOption>
        {
            new(LicenseKind.Monthly, "ترخيص شهري"),
            new(LicenseKind.Permanent, "ترخيص دائم")
        };
        SelectedLicenseKind = LicenseKinds[0];
        DeviceId = licenseService.DeviceId;
    }

    public event EventHandler? Activated;

    public ObservableCollection<LicenseKindOption> LicenseKinds { get; }

    public async Task RefreshStatusAsync()
    {
        try
        {
            ApplyStatus(await _licenseService.GetStatusAsync());
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning("License status refresh failed: {0}", exception);
            LicenseStatusText = "تعذر قراءة حالة الترخيص.";
        }
    }

    [RelayCommand]
    private async Task GenerateRequestAsync()
    {
        if (SelectedLicenseKind is null)
            return;

        if (IsPermanent)
        {
            RequestMessage = "الجهاز مفعل بشكل دائم.";
            return;
        }

        try
        {
            var request = await _licenseService.CreateRequestAsync(SelectedLicenseKind.Kind);
            RequestCode = request.Code;
            DeviceId = request.DeviceId;
            RequestMessage = "تم إنشاء طلب التفعيل. انسخ الرمز وأرسله إلى مزود البرنامج.";
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning("License request creation failed: {0}", exception);
            RequestMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task CopyRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(RequestCode))
        {
            RequestMessage = "أنشئ طلب التفعيل أولاً.";
            return;
        }

        RequestMessage = await CopyToClipboardAsync(RequestCode)
            ? "تم نسخ طلب التفعيل."
            : "تعذر النسخ إلى الحافظة.";
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(ActivationCode))
        {
            ActivationMessage = "أدخل رمز التفعيل أولاً.";
            return;
        }

        try
        {
            var result = await _licenseService.ActivateAsync(ActivationCode);
            ActivationMessage = result.Message;

            if (!result.Success)
                return;

            if (result.Status is not null)
                ApplyStatus(result.Status);

            ActivationCode = string.Empty;
            RequestCode = string.Empty;
            Activated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning("License activation failed: {0}", exception);
            ActivationMessage = "تعذر إكمال التفعيل. حاول مجدداً.";
        }
    }

    [RelayCommand]
    private async Task CopyActivationAsync()
    {
        if (string.IsNullOrWhiteSpace(ActivationCode))
        {
            ActivationMessage = "أدخل رمز التفعيل أولاً.";
            return;
        }

        ActivationMessage = await CopyToClipboardAsync(ActivationCode)
            ? "تم نسخ رمز التفعيل."
            : "تعذر النسخ إلى الحافظة.";
    }

    [RelayCommand]
    private Task RefreshStatus() => RefreshStatusAsync();

    private void ApplyStatus(LicenseStatus status)
    {
        LicenseStatusText = status.Message;
        DeviceId = status.DeviceId;
        IsTrial = status.IsTrial;
        IsActivated = status.IsActivated;
        IsPermanent = status.IsPermanent;
        IsLocked = status.IsLocked;
        TrialDaysRemaining = status.TrialDaysRemaining;
        CanRequestActivation = !status.IsPermanent;
        LockMessage = status.IsLocked ? status.Message : string.Empty;
        ExpiryText = status.ExpiresUtc.HasValue
            ? $"تاريخ الانتهاء: {status.ExpiresUtc.Value.ToLocalTime():yyyy-MM-dd}"
            : string.Empty;
    }

    private static async Task<bool> CopyToClipboardAsync(string value)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetText(value);
                return true;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceWarning("Clipboard copy failed: {0}", exception);
                await Task.Delay(100);
            }
        }

        return false;
    }
}

public sealed record LicenseKindOption(LicenseKind Kind, string DisplayName);
