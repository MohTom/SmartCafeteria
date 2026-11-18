using System.Windows;

namespace SmartCafeteria.ViewModels;

/// <summary>Shared destructive-action prompts. Defaults to "No" so a stray
/// Enter key can never delete data.</summary>
internal static class Confirmations
{
    public static bool Delete(string description)
        => MessageBox.Show(
            $"هل تريد حذف {description}؟\nلا يمكن التراجع عن هذا الإجراء.",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
