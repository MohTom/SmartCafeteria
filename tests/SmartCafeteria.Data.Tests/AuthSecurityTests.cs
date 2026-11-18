using System.Security.Cryptography;
using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Validation;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.TestSupport;
using Xunit;

namespace SmartCafeteria.Data.Tests;

/// <summary>Security behaviour of the account layer: lockout, hashing, password
/// policy, forced change and administrator-only management.</summary>
public class AuthSecurityTests : IDisposable
{
    private const string LegacyPassword = "admin123";
    private const string ValidPassword = "Cashier!Pass123";
    private const string OtherValidPassword = "Another!Pass456";

    private readonly string _dbPath;
    private readonly DbContext _dbContext;

    public AuthSecurityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_auth_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(_dbContext).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    private UserRepository CreateRepository(TestCurrentUser? currentUser = null, TimeProvider? timeProvider = null)
        => new(_dbContext, currentUser ?? TestCurrentUser.Admin(), new NullLogService(), timeProvider);

    private static string CreateLegacyHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt.Concat(hash).ToArray());
    }

    [Fact]
    public void PasswordHasher_ProducesVersionedHash_AndVerifies()
    {
        var hash = PasswordHasher.HashPassword(ValidPassword);

        Assert.StartsWith("v2$600000$", hash);
        Assert.True(PasswordHasher.VerifyPassword(ValidPassword, hash));
        Assert.False(PasswordHasher.VerifyPassword("something-else!", hash));
        Assert.False(PasswordHasher.NeedsRehash(hash));
    }

    [Fact]
    public void PasswordHasher_VerifiesLegacyHash_AndFlagsRehash()
    {
        var legacy = CreateLegacyHash(ValidPassword);

        Assert.True(PasswordHasher.VerifyPassword(ValidPassword, legacy));
        Assert.False(PasswordHasher.VerifyPassword("wrongpassword1", legacy));
        Assert.True(PasswordHasher.NeedsRehash(legacy));
    }

    [Fact]
    public void PasswordHasher_RejectsGarbage()
    {
        Assert.False(PasswordHasher.VerifyPassword(ValidPassword, ""));
        Assert.False(PasswordHasher.VerifyPassword(ValidPassword, "not-a-hash"));
        Assert.False(PasswordHasher.VerifyPassword(ValidPassword, "v2$abc$def$ghi"));
        Assert.False(PasswordHasher.VerifyPassword(ValidPassword, "v2$1000$AAAA$AAAA"));
    }

    [Fact]
    public async Task Authenticate_LegacyHash_UpgradesToV2()
    {
        using (var conn = await _dbContext.GetConnectionAsync())
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                  VALUES ('legacy-1', 'legacy', @Hash, 'قديم', @Role, 1, @Now)",
                new { Hash = CreateLegacyHash(ValidPassword), Role = (int)UserRole.Admin, Now = DateTime.Now });
        }

        var repo = CreateRepository(TestCurrentUser.Anonymous());
        var user = await repo.AuthenticateAsync("legacy", ValidPassword);
        Assert.NotNull(user);

        using var check = await _dbContext.GetConnectionAsync();
        var upgraded = await check.ExecuteScalarAsync<string>("SELECT PasswordHash FROM Users WHERE Id = 'legacy-1'");
        Assert.NotNull(upgraded);
        Assert.StartsWith("v2$", upgraded);
        Assert.True(PasswordHasher.VerifyPassword(ValidPassword, upgraded));
    }

    [Fact]
    public async Task Authenticate_FiveFailures_LocksAccount_ThenExpires()
    {
        var time = new ManualTimeProvider();
        var repo = CreateRepository(TestCurrentUser.Anonymous(), time);
        await repo.CreateUserAsync(new User { Id = "lock-1", Username = "locked", FullName = "مقفل", Role = UserRole.Cashier }, ValidPassword);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failure = await repo.AuthenticateDetailedAsync("locked", "WrongPass!123");
            Assert.Equal(LoginFailureReason.InvalidCredentials, failure.FailureReason);
        }

        Assert.NotNull(await repo.AuthenticateAsync("locked", ValidPassword));

        for (var attempt = 1; attempt <= 5; attempt++)
            await repo.AuthenticateDetailedAsync("locked", "WrongPass!123");

        var locked = await repo.AuthenticateDetailedAsync("locked", ValidPassword);
        Assert.Equal(LoginFailureReason.LockedOut, locked.FailureReason);
        Assert.True(locked.LockoutRemaining > TimeSpan.Zero);
        Assert.Null(locked.User);

        time.Advance(TimeSpan.FromMinutes(16));

        var afterExpiry = await repo.AuthenticateAsync("locked", ValidPassword);
        Assert.NotNull(afterExpiry);
    }

    [Fact]
    public async Task Authenticate_InactiveUser_ReportsInactive()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "inactive-1", Username = "inactive", FullName = "موقوف", Role = UserRole.Cashier }, ValidPassword);
        await repo.SetActiveAsync("inactive-1", false);

        var result = await repo.AuthenticateDetailedAsync("inactive", ValidPassword);
        Assert.Equal(LoginFailureReason.Inactive, result.FailureReason);
    }

    [Fact]
    public async Task ChangePassword_ClearsMustChangeFlag()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User
        {
            Id = "must-1",
            Username = "mustchange",
            FullName = "إجباري",
            Role = UserRole.Cashier,
            MustChangePassword = true
        }, ValidPassword);

        Assert.True((await repo.GetUserByIdAsync("must-1"))!.MustChangePassword);

        Assert.True(await repo.ChangePasswordAsync("must-1", ValidPassword, OtherValidPassword));
        Assert.False((await repo.GetUserByIdAsync("must-1"))!.MustChangePassword);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("12345678901")]
    public async Task CreateUser_AcceptsShortNonEmptyPassword(string password)
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Username = "policytest", FullName = "سياسة", Role = UserRole.Cashier }, password);

        Assert.NotNull(await repo.GetUserByUsernameAsync("policytest"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateUser_RejectsEmptyPassword(string? password)
    {
        var repo = CreateRepository();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.CreateUserAsync(new User { Username = "policytest", FullName = "سياسة", Role = UserRole.Cashier }, password!));
    }

    [Fact]
    public async Task ChangePassword_AcceptsShortNonEmptyPassword()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User { Id = "policy-1", Username = "policy1", FullName = "سياسة", Role = UserRole.Cashier }, ValidPassword);

        Assert.True(await repo.ChangePasswordAsync("policy-1", ValidPassword, "short"));
    }

    [Fact]
    public void PasswordPolicy_RequiresOnlyANonEmptyPassword()
    {
        Assert.True(PasswordPolicy.IsValid("short"));
        Assert.True(PasswordPolicy.IsValid("12345678901"));
        Assert.False(PasswordPolicy.IsValid(string.Empty));
        Assert.False(PasswordPolicy.IsValid(null));
        Assert.Null(PasswordPolicy.GetError("a"));
        Assert.NotNull(PasswordPolicy.GetError(string.Empty));
    }

    [Fact]
    public async Task Reads_NeverExposePasswordHash()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "proj-1", Username = "projection", FullName = "عرض", Role = UserRole.Cashier }, ValidPassword);

        Assert.True(string.IsNullOrEmpty((await repo.GetUserByIdAsync("proj-1"))!.PasswordHash));
        Assert.True(string.IsNullOrEmpty((await repo.GetUserByUsernameAsync("projection"))!.PasswordHash));
        Assert.All(await repo.GetAllUsersAsync(), u => Assert.True(string.IsNullOrEmpty(u.PasswordHash)));
    }

    [Fact]
    public async Task CreateUser_AfterBootstrap_RequiresAdmin()
    {
        var cashier = TestCurrentUser.Cashier("cashier-actor");
        var repo = CreateRepository(cashier);

        await repo.CreateUserAsync(new User { Id = "boot-1", Username = "bootstrap", FullName = "أول", Role = UserRole.Admin }, ValidPassword);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repo.CreateUserAsync(new User { Id = "boot-2", Username = "second", FullName = "ثاني", Role = UserRole.Cashier }, ValidPassword));
    }

    [Fact]
    public async Task Anonymous_CannotCreateSecondUser()
    {
        var repo = CreateRepository(TestCurrentUser.Anonymous());
        await repo.CreateUserAsync(new User { Id = "anon-1", Username = "first", FullName = "أول", Role = UserRole.Admin }, ValidPassword);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            repo.CreateUserAsync(new User { Id = "anon-2", Username = "second", FullName = "ثاني", Role = UserRole.Cashier }, ValidPassword));
    }

    [Fact]
    public async Task Cashier_CannotMutateAccountingSuppliersOrEmployees()
    {
        var adminRepo = CreateRepository();
        await adminRepo.CreateUserAsync(new User { Id = "cashier-x", Username = "cashierx", FullName = "كاشير", Role = UserRole.Admin }, ValidPassword);

        var cashier = TestCurrentUser.Cashier("cashier-x");
        var accountRepo = new AccountRepository(_dbContext, new OrderRepository(_dbContext, new ProductRepository(_dbContext)), cashier);
        var supplierRepo = new SupplierRepository(_dbContext, cashier);
        var employeeRepo = new EmployeeRepository(_dbContext, cashier);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            accountRepo.AddEntryAsync(new AccountEntry { Id = "e1", Amount = 1, Date = DateTime.Now }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            supplierRepo.AddSupplierAsync(new Supplier { Id = "s1", Name = "مورد" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            employeeRepo.AddEmployeeAsync(new Employee { Id = "emp1", Name = "موظف" }));
    }

    [Fact]
    public async Task LastAdmin_CannotBeDeletedOrDeactivated()
    {
        var repo = CreateRepository();
        await repo.CreateUserAsync(new User { Id = "only-admin", Username = "onlyadmin", FullName = "مدير", Role = UserRole.Admin }, ValidPassword);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteUserAsync("only-admin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SetActiveAsync("only-admin", false));
        Assert.NotNull(await repo.GetUserByIdAsync("only-admin"));
    }

    [Fact]
    public async Task Admin_CannotDeleteOrDeactivateSelf()
    {
        var repo = CreateRepository(TestCurrentUser.Admin("self"));
        await repo.CreateUserAsync(new User { Id = "self", Username = "self", FullName = "نفسي", Role = UserRole.Admin }, ValidPassword);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteUserAsync("self"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SetActiveAsync("self", false));
    }

    [Fact]
    public async Task Admin_CannotDemoteSelf()
    {
        var repo = CreateRepository(TestCurrentUser.Admin("self2"));
        await repo.CreateUserAsync(new User { Id = "self2", Username = "self2", FullName = "نفسي", Role = UserRole.Admin }, ValidPassword);

        var me = await repo.GetUserByIdAsync("self2");
        me!.Role = UserRole.Cashier;

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateUserAsync(me));
    }

    [Fact]
    public async Task DatabaseInitializer_FlagsLegacyDefaultAdminPassword()
    {
        using (var conn = await _dbContext.GetConnectionAsync())
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                  VALUES ('legacy-admin', 'admin', @Hash, 'مدير', @Role, 1, @Now)",
                new { Hash = CreateLegacyHash(LegacyPassword), Role = (int)UserRole.Admin, Now = DateTime.Now });
            await conn.ExecuteAsync(
                @"INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                  VALUES ('legacy-cashier', 'cashier', @Hash, 'كاشير', @Role, 1, @Now)",
                new { Hash = CreateLegacyHash(LegacyPassword), Role = (int)UserRole.Cashier, Now = DateTime.Now });
        }

        await new DatabaseInitializer(_dbContext).InitializeAsync();

        using var check = await _dbContext.GetConnectionAsync();
        Assert.Equal(1, await check.ExecuteScalarAsync<int>("SELECT MustChangePassword FROM Users WHERE Id = 'legacy-admin'"));
        Assert.Equal(0, await check.ExecuteScalarAsync<int>("SELECT MustChangePassword FROM Users WHERE Id = 'legacy-cashier'"));
    }

    [Fact]
    public async Task DatabaseInitializer_LeavesChangedAdminPasswordAlone()
    {
        using (var conn = await _dbContext.GetConnectionAsync())
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                  VALUES ('changed-admin', 'admin', @Hash, 'مدير', @Role, 1, @Now)",
                new { Hash = PasswordHasher.HashPassword(ValidPassword), Role = (int)UserRole.Admin, Now = DateTime.Now });
        }

        await new DatabaseInitializer(_dbContext).InitializeAsync();

        using var check = await _dbContext.GetConnectionAsync();
        Assert.Equal(0, await check.ExecuteScalarAsync<int>("SELECT MustChangePassword FROM Users WHERE Id = 'changed-admin'"));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }
}
