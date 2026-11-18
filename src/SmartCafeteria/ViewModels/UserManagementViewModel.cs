using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Validation;
using SmartCafeteria.Services;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;

public partial class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly ILogService _logService;
    private readonly UserSession _userSession;

    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newFullName = string.Empty;
    [ObservableProperty] private UserRole _newRole = UserRole.Cashier;
    [ObservableProperty] private string _newPassword = string.Empty;

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    public UserManagementViewModel(IUserService userService, ILogService logService, UserSession userSession)
    {
        _userService = userService;
        _logService = logService;
        _userSession = userSession;
    }

    public Task LoadUsersAsync()
    {
        return RefreshUsersAsync();
    }

    private async Task RefreshUsersAsync()
    {
        var users = await _userService.GetAllUsersAsync();
        Users = new ObservableCollection<User>(users);
    }

    public ObservableCollection<UserRole> Roles { get; } = new() { UserRole.Admin, UserRole.Cashier };

    [RelayCommand]
    private async Task AddUserAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            ShowError("الرجاء إدخال اسم المستخدم");
            return;
        }
        if (string.IsNullOrWhiteSpace(NewFullName))
        {
            ShowError("الرجاء إدخال الاسم الكامل");
            return;
        }
        if (string.IsNullOrEmpty(NewPassword))
        {
            ShowError("الرجاء إدخال كلمة المرور");
            return;
        }
        var policyError = PasswordPolicy.GetError(NewPassword);
        if (policyError != null)
        {
            ShowError(policyError);
            return;
        }
        if (await _userService.UserExistsAsync(NewUsername.Trim()))
        {
            ShowError("اسم المستخدم موجود بالفعل");
            return;
        }

        var user = new User
        {
            Username = NewUsername.Trim(),
            FullName = NewFullName.Trim(),
            Role = NewRole,
            MustChangePassword = true
        };

        try
        {
            await _userService.CreateUserAsync(user, NewPassword);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError(ex.Message);
            return;
        }

        await _logService.AddLogAsync($"تمت إضافة المستخدم: {user.FullName}", "success");
        StatusMessage = "تمت إضافة المستخدم بنجاح";
        ClearForm();
        await RefreshUsersAsync();
    }

    [RelayCommand]
    private async Task UpdateUserAsync()
    {
        if (SelectedUser == null)
        {
            ShowError("الرجاء تحديد مستخدم للتعديل");
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedUser.FullName))
        {
            ShowError("اسم المستخدم الكامل لا يمكن أن يكون فارغاً");
            return;
        }
        if (await _userService.UserExistsAsync(SelectedUser.Username, SelectedUser.Id))
        {
            ShowError("اسم المستخدم موجود بالفعل");
            return;
        }

        try
        {
            await _userService.UpdateUserAsync(SelectedUser);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            await RefreshUsersAsync();
            return;
        }

        await _logService.AddLogAsync($"تم تعديل المستخدم: {SelectedUser.FullName}", "info");
        StatusMessage = "تم حفظ التعديلات بنجاح";
        await RefreshUsersAsync();
    }

    [RelayCommand]
    private async Task DeleteUserAsync(User? user)
    {
        if (user == null)
            user = SelectedUser;
        if (user == null)
            return;

        if (_userSession.CurrentUser?.Id == user.Id)
        {
            ShowError("لا يمكن حذف حسابك الحالي");
            return;
        }
        if (user.Role == UserRole.Admin && Users.Count(u => u.Role == UserRole.Admin) <= 1)
        {
            ShowError("لا يمكن حذف آخر حساب بمهنة مدير");
            return;
        }
        if (!Confirmations.Delete($"حساب المستخدم {user.Username}")) return;

        try
        {
            await _userService.DeleteUserAsync(user.Id);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            await RefreshUsersAsync();
            return;
        }

        await _logService.AddLogAsync($"تم حذف المستخدم: {user.FullName}", "warning");
        StatusMessage = "تم حذف المستخدم";
        await RefreshUsersAsync();
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(User? user)
    {
        if (user == null)
            return;
        if (_userSession.CurrentUser?.Id == user.Id)
        {
            ShowError("لا يمكن تعطيل حسابك الحالي");
            return;
        }

        user.IsActive = !user.IsActive;
        try
        {
            await _userService.SetActiveAsync(user.Id, user.IsActive);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            await RefreshUsersAsync();
            return;
        }

        await _logService.AddLogAsync(
            $"{(user.IsActive ? "تم تفعيل" : "تم تعطيل")} المستخدم: {user.FullName}", "info");
        await RefreshUsersAsync();
    }

    private void ClearForm()
    {
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewPassword = string.Empty;
        NewRole = UserRole.Cashier;
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}