using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Activator.Services;
using SmartCafeteria.Licensing.Codec;
using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.Service;

namespace SmartCafeteria.Activator.ViewModels;

public partial class ActivatorViewModel : ObservableObject
{
    [ObservableProperty] private string _requestCode = string.Empty;
    [ObservableProperty] private string _activationCode = string.Empty;
    [ObservableProperty] private string _detailsText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _keyPath = string.Empty;

    public ActivatorViewModel()
    {
        var defaultKey = Path.Combine(AppContext.BaseDirectory, "keys", "activator-private-key.pk8");
        if (File.Exists(defaultKey))
            KeyPath = defaultKey;
    }

    public string Password { get; set; } = string.Empty;

    [RelayCommand]
    private void BrowseKey()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر ملف المفتاح الخاص",
            Filter = "Private key (*.pk8)|*.pk8|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
            KeyPath = dialog.FileName;
    }

    [RelayCommand]
    private void Generate()
    {
        ActivationCode = string.Empty;
        DetailsText = string.Empty;
        StatusMessage = string.Empty;

        if (!RequestCodec.TryDecode(RequestCode, out var request, out var requestError))
        {
            StatusMessage = requestError;
            return;
        }

        if (request is null)
        {
            StatusMessage = "طلب التفعيل غير صالح.";
            return;
        }

        using var key = PrivateKeyLoader.Load(KeyPath, Password, out var keyError);
        if (key is null)
        {
            StatusMessage = keyError;
            return;
        }

        try
        {
            var issueUtc = DateTimeOffset.Now;
            var expiry = request.Kind == LicenseKind.Monthly
                ? LicensePolicy.MonthlyExpiry(issueUtc)
                : (DateTimeOffset?)null;

            var issuer = new EcdsaLicenseIssuer(key);
            ActivationCode = issuer.Issue(
                request.Kind,
                issueUtc,
                expiry,
                request.HardwareId,
                request.Nonce);

            var lines = new List<string>
            {
                $"النوع: {(request.Kind == LicenseKind.Monthly ? "شهري" : "دائم")}",
                $"معرف الجهاز: {request.DeviceId}",
                $"عنوان MAC: {request.MacAddress}",
                $"تاريخ الطلب: {request.RequestedUtc.ToLocalTime():yyyy-MM-dd}",
                $"تاريخ الإصدار: {issueUtc.ToLocalTime():yyyy-MM-dd}"
            };

            if (expiry.HasValue)
                lines.Add($"تاريخ الانتهاء: {expiry.Value.ToLocalTime():yyyy-MM-dd}");

            if (request.CurrentExpiryUtc.HasValue &&
                expiry.HasValue &&
                expiry.Value <= request.CurrentExpiryUtc.Value)
            {
                lines.Add("تحذير: هذا الرمز لا يمدد الصلاحية الحالية وسيرفضه البرنامج.");
            }

            DetailsText = string.Join(Environment.NewLine, lines);
            StatusMessage = "تم إنشاء رمز التفعيل. انسخه وأرسله إلى العميل.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"تعذر إنشاء رمز التفعيل: {exception.Message}";
        }
    }

    [RelayCommand]
    private void CopyActivation()
    {
        if (string.IsNullOrWhiteSpace(ActivationCode))
        {
            StatusMessage = "أنشئ رمز التفعيل أولاً.";
            return;
        }

        try
        {
            Clipboard.SetText(ActivationCode);
            StatusMessage = "تم نسخ رمز التفعيل.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"تعذر النسخ إلى الحافظة: {exception.Message}";
        }
    }
}
