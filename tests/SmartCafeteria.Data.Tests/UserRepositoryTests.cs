using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.TestSupport;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class UserRepositoryTests : IDisposable
{
    private const string ValidPassword = "Cashier!Pass123";
    private const string NewValidPassword = "NewPass!456789";

    private readonly string _dbPath;
    private readonly DbContext _dbContext;

    public UserRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_user_test_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(_dbContext).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private UserRepository CreateRepository(TestCurrentUser? currentUser = null)
        => new(_dbContext, currentUser ?? TestCurrentUser.Admin(), new NullLogService());

    [Fact]
    public async Task UserRepository_CreateAndGetAll()
    {
        var repo = CreateRepository();
        var user = new User { Id = "u1", Username = "cashier1", FullName = "كاشير واحد", Role = UserRole.Cashier };
        await repo.CreateUserAsync(user, ValidPassword);

        var all = await repo.GetAllUsersAsync();
        var saved = Assert.Single(all, u => u.Id == "u1");
        Assert.Equal("cashier1", saved.Username);
        Assert.Equal("كاشير واحد", saved.FullName);
        Assert.Equal(UserRole.Cashier, saved.Role);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task UserRepository_Create_DoesNotMutateInput()
    {
        var repo = CreateRepository();
        var user = new User { Username = "  unmutated  ", FullName = "بدون تعديل", Role = UserRole.Cashier };

        var created = await repo.CreateUserAsync(user, ValidPassword);

        Assert.Equal("  unmutated  ", user.Username);
        Assert.Equal("unmutated", created.Username);
        Assert.True(string.IsNullOrEmpty(user.PasswordHash));
        Assert.NotSame(user, created);
    }

    [Fact]
    public async Task UserRepository_HashPassword_IsNotPlainText()
    {
        var repo = CreateRepository();
        var user = new User { Id = "u2", Username = "cashier2", FullName = "كاشير اثنان", Role = UserRole.Cashier };
        await repo.CreateUserAsync(user, "abc123456789");

        var saved = await repo.GetUserByIdAsync("u2");
        Assert.NotNull(saved);
        Assert.True(string.IsNullOrEmpty(saved.PasswordHash));

        using var conn = await _dbContext.GetConnectionAsync();
        var hash = await conn.ExecuteScalarAsync<string>("SELECT PasswordHash FROM Users WHERE Id = 'u2'");
        Assert.NotNull(hash);
        Assert.StartsWith("v2$", hash);
        Assert.DoesNotContain("abc123456789", hash);
    }

    [Fact]
    public async Task UserRepository_Authenticate_Success()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User { Id = "u3", Username = "admin1", FullName = "مدير", Role = UserRole.Admin }, ValidPassword);

        var user = await repo.AuthenticateAsync("admin1", ValidPassword);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Admin, user!.Role);
        Assert.True(string.IsNullOrEmpty(user.PasswordHash));
    }

    [Fact]
    public async Task UserRepository_Authenticate_WrongPassword_Fails()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User { Id = "u4", Username = "cashier4", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        var user = await repo.AuthenticateAsync("cashier4", "wrongpass1234");
        Assert.Null(user);
    }

    [Fact]
    public async Task UserRepository_Authenticate_UnknownUser_Fails()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        var user = await repo.AuthenticateAsync("nobody", "pass12345678");
        Assert.Null(user);
    }

    [Fact]
    public async Task UserRepository_UpdateUser()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "u5", Username = "c5", FullName = "أول", Role = UserRole.Cashier }, ValidPassword);

        var user = await repo.GetUserByIdAsync("u5");
        Assert.NotNull(user);
        user!.FullName = "معدل";
        user.Role = UserRole.Admin;
        await repo.UpdateUserAsync(user);

        var updated = await repo.GetUserByIdAsync("u5");
        Assert.NotNull(updated);
        Assert.Equal("معدل", updated!.FullName);
        Assert.Equal(UserRole.Admin, updated.Role);
    }

    [Fact]
    public async Task UserRepository_ChangePassword()
    {
        var repo = CreateRepository(TestCurrentUser.Admin("u6"));
        await repo.CreateUserAsync(new User { Id = "u6", Username = "c6", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        var changed = await repo.ChangePasswordAsync("u6", NewValidPassword);
        Assert.True(changed);

        var oldAuth = await repo.AuthenticateAsync("c6", ValidPassword);
        Assert.Null(oldAuth);

        var newAuth = await repo.AuthenticateAsync("c6", NewValidPassword);
        Assert.NotNull(newAuth);
    }

    [Fact]
    public async Task UserRepository_ChangePassword_WithOldPassword()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User { Id = "u6b", Username = "c6b", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        Assert.False(await repo.ChangePasswordAsync("u6b", "Wrong!OldPass1", NewValidPassword));
        Assert.True(await repo.ChangePasswordAsync("u6b", ValidPassword, NewValidPassword));
        Assert.NotNull(await repo.AuthenticateAsync("c6b", NewValidPassword));
    }

    [Fact]
    public async Task UserRepository_SetActive_False_BlocksLogin()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "u7", Username = "c7", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        await repo.SetActiveAsync("u7", false);

        var auth = await repo.AuthenticateAsync("c7", ValidPassword);
        Assert.Null(auth);
    }

    [Fact]
    public async Task UserRepository_DeleteUser()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "u8", Username = "c8", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        var deleted = await repo.DeleteUserAsync("u8");
        Assert.True(deleted);

        var user = await repo.GetUserByIdAsync("u8");
        Assert.Null(user);
    }

    [Fact]
    public async Task UserRepository_UserExists_DetectsDuplicate()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "u9", Username = "dup", FullName = "كاشير", Role = UserRole.Cashier }, ValidPassword);

        var exists = await repo.UserExistsAsync("dup");
        Assert.True(exists);

        var existsSelf = await repo.UserExistsAsync("dup", "u9");
        Assert.False(existsSelf);

        var nonExists = await repo.UserExistsAsync("nope");
        Assert.False(nonExists);
    }

    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Cashier", UserRole.Cashier)]
    public async Task UserRepository_AddAdminAndCashierRoles(string username, UserRole role)
    {
        var repo = CreateRepository();
        var id = $"u-{username.ToLower()}";
        await repo.CreateUserAsync(new User { Id = id, Username = username.ToLower(), FullName = username, Role = role }, ValidPassword);

        var all = await repo.GetAllUsersAsync();
        var saved = Assert.Single(all, u => u.Id == id);
        Assert.Equal(role, saved.Role);
    }
}
