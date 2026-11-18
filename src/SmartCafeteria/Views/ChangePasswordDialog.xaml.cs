using System;
using System.Threading.Tasks;
using System.Windows;
using SmartCafeteria.Core.Validation;

namespace SmartCafeteria.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly Func<string, string, Task<string?>> _changePassword;

    public ChangePasswordDialog(string username, Func<string, string, Task<string?>> changePassword)
    {
        InitializeComponent();
        _changePassword = changePassword;
        HintText.Text = $"يجب تعيين كلمة مرور جديدة للحساب \"{username}\" قبل المتابعة.";
        PolicyHint.Text = PasswordPolicy.GetError(string.Empty);
        Loaded += (_, _) => CurrentPasswordInput.Focus();
    }

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var current = CurrentPasswordInput.Password;
        var next = NewPasswordInput.Password;
        var confirm = ConfirmPasswordInput.Password;

        if (string.IsNullOrEmpty(current))
        {
            ShowError("الرجاء إدخال كلمة المرور الحالية");
            return;
        }

        var policyError = PasswordPolicy.GetError(next);
        if (policyError != null)
        {
            ShowError(policyError);
            return;
        }

        if (next != confirm)
        {
            ShowError("تأكيد كلمة المرور غير مطابق");
            return;
        }

        ConfirmButton.IsEnabled = false;
        try
        {
            var error = await _changePassword(current, next);
            if (error != null)
            {
                ShowError(error);
                return;
            }

            DialogResult = true;
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
