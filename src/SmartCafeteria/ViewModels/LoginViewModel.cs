using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Validation;
using SmartCafeteria.Services;

namespace SmartCafeteria.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly UserSession _userSession;
    private readonly ISettingsService _settingsService;

    public event EventHandler? LoginSucceeded;

    /// <summary>Raised after a correct password when the account is flagged to
    /// choose a new password before it may enter the app.</summary>
    public event EventHandler<User>? PasswordChangeRequired;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private AppSettings _settings = new();

    public LoginViewModel(IUserService userService, UserSession userSession, ISettingsService settingsService)
    {
        _userService = userService;
        _userSession = userSession;
        _settingsService = settingsService;
    }

    public async Task LoadSettingsAsync()
    {
        Settings = await _settingsService.GetSettingsAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            ShowError("الرجاء إدخال اسم المستخدم وكلمة المرور");
            return;
        }

        var result = await _userService.AuthenticateDetailedAsync(Username.Trim(), Password);
        if (!result.Succeeded)
        {
            ShowError(DescribeFailure(result));
            return;
        }

        Password = string.Empty;
        CompleteLogin(result.User!);
    }

    /// <summary>Called after the forced password change succeeded: the account is
    /// re-read so the session never carries the stale must-change flag.</summary>
    public async Task CompleteLoginAfterPasswordChangeAsync(User user)
    {
        var refreshed = await _userService.GetUserByIdAsync(user.Id) ?? user;
        CompleteLogin(refreshed);
    }

    /// <summary>Validates and applies the forced change. Returns an Arabic error
    /// message on failure, or null when the password was changed.</summary>
    public async Task<string?> ChangeRequiredPasswordAsync(string userId, string currentPassword, string newPassword)
    {
        if (!PasswordPolicy.IsValid(newPassword))
            return PasswordPolicy.GetError(newPassword);

        if (currentPassword == newPassword)
            return "كلمة المرور الجديدة يجب أن تختلف عن كلمة المرور الحالية";

        try
        {
            var changed = await _userService.ChangePasswordAsync(userId, currentPassword, newPassword);
            return changed ? null : "كلمة المرور الحالية غير صحيحة";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private void CompleteLogin(User user)
    {
        if (user.MustChangePassword)
        {
            PasswordChangeRequired?.Invoke(this, user);
            return;
        }

        _userSession.SetCurrentUser(user);
        LoginSucceeded?.Invoke(this, EventArgs.Empty);
    }

    private static string DescribeFailure(LoginResult result)
    {
        switch (result.FailureReason)
        {
            case LoginFailureReason.LockedOut:
                var minutes = Math.Max(1, (int)Math.Ceiling(result.LockoutRemaining.TotalMinutes));
                return $"تم إيقاف الحساب مؤقتاً بسبب محاولات دخول خاطئة. حاول بعد {minutes} دقيقة";
            case LoginFailureReason.Inactive:
                return "هذا الحساب غير نشط، راجع مدير النظام";
            default:
                return "اسم المستخدم أو كلمة المرور غير صحيحة";
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}
