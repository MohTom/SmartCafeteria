using Moq;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using SmartCafeteria.ViewModels;
using Xunit;

namespace SmartCafeteria.ViewModels.Tests;

public class UserManagementViewModelTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly UserSession _session = new();

    private UserManagementViewModel CreateViewModel(string actorId = "admin-1")
    {
        _session.SetCurrentUser(new User
        {
            Id = actorId,
            Username = "admin",
            FullName = "مدير",
            Role = UserRole.Admin,
            IsActive = true
        });
        return new UserManagementViewModel(_userService.Object, _logService.Object, _session);
    }

    [Fact]
    public async Task AddUser_AcceptsShortPassword()
    {
        _userService.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(Array.Empty<User>());
        _userService.Setup(s => s.UserExistsAsync("newcashier", null)).ReturnsAsync(false);
        _userService
            .Setup(s => s.CreateUserAsync(It.IsAny<User>(), "short"))
            .ReturnsAsync((User u, string _) => u);
        var vm = CreateViewModel();
        vm.NewUsername = "newcashier";
        vm.NewFullName = "كاشير جديد";
        vm.NewPassword = "short";

        await vm.AddUserCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        _userService.Verify(
            s => s.CreateUserAsync(It.IsAny<User>(), "short"),
            Times.Once);
    }

    [Fact]
    public async Task AddUser_ValidPassword_CreatesUserWithForcedChange()
    {
        _userService.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(Array.Empty<User>());
        _userService.Setup(s => s.UserExistsAsync("newcashier", null)).ReturnsAsync(false);
        User? created = null;
        _userService
            .Setup(s => s.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()))
            .Callback<User, string>((user, _) => created = user)
            .ReturnsAsync((User u, string _) => u);

        var vm = CreateViewModel();
        vm.NewUsername = "newcashier";
        vm.NewFullName = "كاشير جديد";
        vm.NewPassword = "Cashier!Pass123";

        await vm.AddUserCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.NotNull(created);
        Assert.True(created!.MustChangePassword);
        Assert.Equal(string.Empty, vm.NewPassword);
    }

    [Fact]
    public async Task AddUser_DuplicateUsername_ShowsError()
    {
        _userService.Setup(s => s.UserExistsAsync("taken", null)).ReturnsAsync(true);
        var vm = CreateViewModel();
        vm.NewUsername = "taken";
        vm.NewFullName = "مستخدم";
        vm.NewPassword = "Cashier!Pass123";

        await vm.AddUserCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("موجود", vm.ErrorMessage);
    }

    [Fact]
    public async Task AddUser_ServiceRejects_ShowsServiceMessage()
    {
        _userService.Setup(s => s.UserExistsAsync("racer", null)).ReturnsAsync(false);
        _userService
            .Setup(s => s.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Username 'racer' already exists."));

        var vm = CreateViewModel();
        vm.NewUsername = "racer";
        vm.NewFullName = "مستخدم";
        vm.NewPassword = "Cashier!Pass123";

        await vm.AddUserCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("already exists", vm.ErrorMessage);
    }

    [Fact]
    public async Task ToggleActive_Self_IsBlockedBeforeService()
    {
        var vm = CreateViewModel("admin-1");
        var me = _session.CurrentUser!;

        await vm.ToggleActiveCommand.ExecuteAsync(me);

        Assert.True(vm.HasError);
        _userService.Verify(
            s => s.SetActiveAsync(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
        Assert.True(me.IsActive);
    }

    [Fact]
    public async Task ToggleActive_ServiceRejection_RefreshesAndShowsError()
    {
        var expected = new[] { new User { Id = "other", Username = "other", Role = UserRole.Cashier } };
        _userService.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(expected);
        _userService
            .Setup(s => s.SetActiveAsync("other", false))
            .ThrowsAsync(new UnauthorizedAccessException("requires an active administrator"));

        var vm = CreateViewModel();
        var other = new User { Id = "other", Username = "other", Role = UserRole.Cashier, IsActive = true };

        await vm.ToggleActiveCommand.ExecuteAsync(other);

        Assert.True(vm.HasError);
        Assert.Contains("administrator", vm.ErrorMessage);
        Assert.Single(vm.Users);
    }
}
