using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using SmartCafeteria.ViewModels;
using Xunit;

namespace SmartCafeteria.ViewModels.Tests;

public class LoginViewModelTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<ISettingsService> _settingsService = new();
    private readonly UserSession _session = new();

    private LoginViewModel CreateViewModel() => new(_userService.Object, _session, _settingsService.Object);

    private static User Cashier(bool mustChangePassword = false) => new()
    {
        Id = "u-1",
        Username = "cashier",
        FullName = "كاشير",
        Role = UserRole.Cashier,
        MustChangePassword = mustChangePassword
    };

    [Fact]
    public async Task Login_Succeeds_SetsSessionAndRaisesEvent()
    {
        var user = Cashier();
        _userService
            .Setup(s => s.AuthenticateDetailedAsync("cashier", "Cashier!Pass123"))
            .ReturnsAsync(LoginResult.Success(user));

        var vm = CreateViewModel();
        vm.Username = " cashier ";
        vm.Password = "Cashier!Pass123";
        var raised = false;
        vm.LoginSucceeded += (_, _) => raised = true;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(raised);
        Assert.Same(user, _session.CurrentUser);
        Assert.False(vm.HasError);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Fact]
    public async Task Login_WrongCredentials_ShowsGenericError()
    {
        _userService
            .Setup(s => s.AuthenticateDetailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(LoginResult.Failed(LoginFailureReason.InvalidCredentials));

        var vm = CreateViewModel();
        vm.Username = "cashier";
        vm.Password = "wrong";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("اسم المستخدم أو كلمة المرور غير صحيحة", vm.ErrorMessage);
        Assert.Null(_session.CurrentUser);
    }

    [Fact]
    public async Task Login_LockedOut_ReportsRemainingMinutes()
    {
        _userService
            .Setup(s => s.AuthenticateDetailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(LoginResult.Failed(LoginFailureReason.LockedOut, TimeSpan.FromMinutes(12.5)));

        var vm = CreateViewModel();
        vm.Username = "cashier";
        vm.Password = "wrong";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("13", vm.ErrorMessage);
        Assert.Contains("دقيقة", vm.ErrorMessage);
    }

    [Fact]
    public async Task Login_Inactive_ReportsInactive()
    {
        _userService
            .Setup(s => s.AuthenticateDetailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(LoginResult.Failed(LoginFailureReason.Inactive));

        var vm = CreateViewModel();
        vm.Username = "cashier";
        vm.Password = "Cashier!Pass123";

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("غير نشط", vm.ErrorMessage);
        Assert.Null(_session.CurrentUser);
    }

    [Fact]
    public async Task Login_MustChangePassword_RequestsChangeWithoutSigningIn()
    {
        var user = Cashier(mustChangePassword: true);
        _userService
            .Setup(s => s.AuthenticateDetailedAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(LoginResult.Success(user));

        var vm = CreateViewModel();
        vm.Username = "cashier";
        vm.Password = "Cashier!Pass123";
        User? requested = null;
        var signedIn = false;
        vm.PasswordChangeRequired += (_, u) => requested = u;
        vm.LoginSucceeded += (_, _) => signedIn = true;

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Same(user, requested);
        Assert.False(signedIn);
        Assert.Null(_session.CurrentUser);
    }

    [Fact]
    public async Task CompleteLoginAfterPasswordChange_RefreshesUserAndSignsIn()
    {
        var stale = Cashier(mustChangePassword: true);
        var refreshed = Cashier();
        _userService.Setup(s => s.GetUserByIdAsync("u-1")).ReturnsAsync(refreshed);

        var vm = CreateViewModel();
        var signedIn = false;
        vm.LoginSucceeded += (_, _) => signedIn = true;

        await vm.CompleteLoginAfterPasswordChangeAsync(stale);

        Assert.True(signedIn);
        Assert.Same(refreshed, _session.CurrentUser);
    }

    [Fact]
    public async Task ChangeRequiredPassword_AcceptsShortPassword()
    {
        _userService
            .Setup(s => s.ChangePasswordAsync("u-1", "Cashier!Pass123", "short"))
            .ReturnsAsync(true);
        var vm = CreateViewModel();

        var error = await vm.ChangeRequiredPasswordAsync("u-1", "Cashier!Pass123", "short");

        Assert.Null(error);
        _userService.Verify(
            s => s.ChangePasswordAsync("u-1", "Cashier!Pass123", "short"),
            Times.Once);
    }

    [Fact]
    public async Task ChangeRequiredPassword_RejectsSamePassword()
    {
        var vm = CreateViewModel();

        var error = await vm.ChangeRequiredPasswordAsync("u-1", "Cashier!Pass123", "Cashier!Pass123");

        Assert.NotNull(error);
        Assert.Contains("تختلف", error);
    }

    [Fact]
    public async Task ChangeRequiredPassword_WrongOldPassword_ReturnsMessage()
    {
        _userService
            .Setup(s => s.ChangePasswordAsync("u-1", "Wrong!OldPass1", "NewPass!456789"))
            .ReturnsAsync(false);

        var vm = CreateViewModel();

        var error = await vm.ChangeRequiredPasswordAsync("u-1", "Wrong!OldPass1", "NewPass!456789");

        Assert.NotNull(error);
        Assert.Contains("الحالية", error);
    }

    [Fact]
    public async Task ChangeRequiredPassword_Succeeds_ReturnsNull()
    {
        _userService
            .Setup(s => s.ChangePasswordAsync("u-1", "Cashier!Pass123", "NewPass!456789"))
            .ReturnsAsync(true);

        var vm = CreateViewModel();

        var error = await vm.ChangeRequiredPasswordAsync("u-1", "Cashier!Pass123", "NewPass!456789");

        Assert.Null(error);
    }

    [Fact]
    public async Task Login_EmptyFields_ShowsValidationError()
    {
        var vm = CreateViewModel();

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("اسم المستخدم", vm.ErrorMessage);
        _userService.Verify(
            s => s.AuthenticateDetailedAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
