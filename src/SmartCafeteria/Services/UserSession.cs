using CommunityToolkit.Mvvm.ComponentModel;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Services;

public partial class UserSession : ObservableObject, ICurrentUserContext
{
    [ObservableProperty] private User? _currentUser;

    public bool IsLoggedIn => CurrentUser != null;
    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public bool IsActiveAdmin => CurrentUser is { IsActive: true, Role: UserRole.Admin };

    partial void OnCurrentUserChanged(User? value)
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsAdmin));
    }

    public void SetCurrentUser(User user) => CurrentUser = user;

    public void Logout() => CurrentUser = null;
}
