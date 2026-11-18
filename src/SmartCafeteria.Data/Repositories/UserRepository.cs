using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.Sqlite;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Validation;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;

public class UserRepository : IUserService
{
    private const int MaxFailedAttempts = 5;
    private const int UniqueConstraintErrorCode = 19;

    /// <summary>Projection used for every read that feeds the UI: the password hash
    /// must never leave the authentication path.</summary>
    private const string PublicColumns =
        "Id, Username, FullName, Role, IsActive, CreatedAt, MustChangePassword, FailedLoginCount, LockoutUntil";

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Verified when the username does not exist so unknown users cost the
    /// same PBKDF2 work as known ones (removes the timing side channel).</summary>
    private static readonly string DummyHash =
        PasswordHasher.HashPassword("timing-equalizer-not-a-real-password");

    private readonly DbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogService _logService;
    private readonly TimeProvider _timeProvider;

    public UserRepository(
        DbContext dbContext,
        ICurrentUserContext currentUser,
        ILogService logService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logService = logService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private static string RequireUsername(string? username)
    {
        var trimmed = (username ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Username is required.", nameof(username));
        return trimmed;
    }

    private void EnsureAdmin(string operation)
    {
        if (!_currentUser.IsActiveAdmin)
            throw new UnauthorizedAccessException($"{operation} requires an active administrator.");
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<User>($"SELECT {PublicColumns} FROM Users ORDER BY CreatedAt DESC");
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<User>(
            $"SELECT {PublicColumns} FROM Users WHERE Id = @Id", new { Id = id });
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<User>(
            $"SELECT {PublicColumns} FROM Users WHERE Username = @Username", new { Username = username });
    }

    public async Task<User> CreateUserAsync(User user, string plainPassword)
    {
        ArgumentNullException.ThrowIfNull(user);

        var username = RequireUsername(user.Username);
        var policyError = PasswordPolicy.GetError(plainPassword);
        if (policyError != null)
            throw new ArgumentException(policyError, nameof(plainPassword));

        using var conn = await _dbContext.GetConnectionAsync();

        // The very first account may be created by the host at first run (no
        // session exists yet). Every later creation requires an active admin.
        var userCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
        if (userCount > 0)
            EnsureAdmin("Creating a user");

        var existing = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE Username = @Username", new { Username = username });
        if (existing > 0)
            throw new InvalidOperationException($"Username '{username}' already exists.");

        var newUser = new User
        {
            Id = string.IsNullOrWhiteSpace(user.Id) ? $"u-{Guid.NewGuid():N}" : user.Id,
            Username = username,
            FullName = user.FullName ?? string.Empty,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt == default ? DateTime.Now : user.CreatedAt,
            MustChangePassword = user.MustChangePassword
        };

        try
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Users
                    (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt,
                     MustChangePassword, FailedLoginCount, LockoutUntil)
                  VALUES
                    (@Id, @Username, @PasswordHash, @FullName, @Role, @IsActive, @CreatedAt,
                     @MustChangePassword, 0, NULL)",
                new
                {
                    newUser.Id,
                    newUser.Username,
                    PasswordHash = PasswordHasher.HashPassword(plainPassword),
                    newUser.FullName,
                    Role = (int)newUser.Role,
                    IsActive = newUser.IsActive ? 1 : 0,
                    newUser.CreatedAt,
                    MustChangePassword = newUser.MustChangePassword ? 1 : 0
                });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == UniqueConstraintErrorCode)
        {
            // Two concurrent creates can both pass the COUNT above; the unique
            // index is the real guard. Translate it to a domain error.
            throw new InvalidOperationException($"Username '{username}' already exists.", ex);
        }

        return newUser;
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        EnsureAdmin("Updating a user");

        var username = RequireUsername(user.Username);
        var actor = _currentUser.CurrentUser!;

        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();

        var target = await conn.QueryFirstOrDefaultAsync<User>(
            $"SELECT {PublicColumns} FROM Users WHERE Id = @Id", new { user.Id }, transaction);
        if (target == null)
            return false;

        if (target.Id == actor.Id && (user.Role != UserRole.Admin || !user.IsActive))
            throw new InvalidOperationException("You cannot demote or deactivate your own account.");

        var affected = await conn.ExecuteAsync(
            "UPDATE Users SET Username = @Username, FullName = @FullName, Role = @Role, IsActive = @IsActive WHERE Id = @Id",
            new
            {
                user.Id,
                Username = username,
                FullName = user.FullName ?? string.Empty,
                Role = (int)user.Role,
                IsActive = user.IsActive ? 1 : 0
            },
            transaction);

        if (target.Role == UserRole.Admin && (user.Role != UserRole.Admin || !user.IsActive))
        {
            var activeAdmins = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Users WHERE Role = @Admin AND IsActive = 1",
                new { Admin = (int)UserRole.Admin }, transaction);
            if (activeAdmins == 0)
                throw new InvalidOperationException("At least one active administrator must remain.");
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string newPlainPassword)
    {
        var actor = _currentUser.CurrentUser;
        if (actor == null || (actor.Id != userId && !_currentUser.IsActiveAdmin))
            throw new UnauthorizedAccessException("Changing another user's password requires an active administrator.");

        return await SetPasswordAsync(userId, newPlainPassword);
    }

    public async Task<bool> ChangePasswordAsync(string userId, string oldPlainPassword, string newPlainPassword)
    {
        // Proving knowledge of the current password is itself the authorization
        // for this operation, so no session is required (first-login change).
        var policyError = PasswordPolicy.GetError(newPlainPassword);
        if (policyError != null)
            throw new ArgumentException(policyError, nameof(newPlainPassword));

        using var conn = await _dbContext.GetConnectionAsync();
        var storedHash = await conn.ExecuteScalarAsync<string?>(
            "SELECT PasswordHash FROM Users WHERE Id = @Id", new { Id = userId });
        if (storedHash == null || !PasswordHasher.VerifyPassword(oldPlainPassword, storedHash))
            return false;

        var affected = await conn.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @Hash, MustChangePassword = 0, FailedLoginCount = 0, LockoutUntil = NULL WHERE Id = @Id",
            new { Id = userId, Hash = PasswordHasher.HashPassword(newPlainPassword) });
        return affected > 0;
    }

    private async Task<bool> SetPasswordAsync(string userId, string newPlainPassword)
    {
        var policyError = PasswordPolicy.GetError(newPlainPassword);
        if (policyError != null)
            throw new ArgumentException(policyError, nameof(newPlainPassword));

        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @Hash, MustChangePassword = 0, FailedLoginCount = 0, LockoutUntil = NULL WHERE Id = @Id",
            new { Id = userId, Hash = PasswordHasher.HashPassword(newPlainPassword) });
        return affected > 0;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        EnsureAdmin("Deleting a user");
        var actor = _currentUser.CurrentUser!;

        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();

        var targetRole = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT Role FROM Users WHERE Id = @Id", new { Id = userId }, transaction);
        if (targetRole == null)
            return false;

        if (userId == actor.Id)
            throw new InvalidOperationException("You cannot delete your own account.");

        await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = userId }, transaction);

        if (targetRole.Value == (int)UserRole.Admin)
        {
            var activeAdmins = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Users WHERE Role = @Admin AND IsActive = 1",
                new { Admin = (int)UserRole.Admin }, transaction);
            if (activeAdmins == 0)
                throw new InvalidOperationException("At least one active administrator must remain.");
        }

        transaction.Commit();
        return true;
    }

    public async Task<bool> SetActiveAsync(string userId, bool isActive)
    {
        EnsureAdmin("Changing a user's status");
        var actor = _currentUser.CurrentUser!;

        if (userId == actor.Id && !isActive)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();

        var targetRole = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT Role FROM Users WHERE Id = @Id", new { Id = userId }, transaction);
        if (targetRole == null)
            return false;

        var affected = await conn.ExecuteAsync(
            "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = userId, IsActive = isActive ? 1 : 0 }, transaction);

        if (targetRole.Value == (int)UserRole.Admin && !isActive)
        {
            var activeAdmins = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Users WHERE Role = @Admin AND IsActive = 1",
                new { Admin = (int)UserRole.Admin }, transaction);
            if (activeAdmins == 0)
                throw new InvalidOperationException("At least one active administrator must remain.");
        }

        transaction.Commit();
        return affected > 0;
    }

    public async Task<User?> AuthenticateAsync(string username, string plainPassword)
        => (await AuthenticateDetailedAsync(username, plainPassword)).User;

    public async Task<LoginResult> AuthenticateDetailedAsync(string username, string plainPassword)
    {
        var name = (username ?? string.Empty).Trim();
        using var conn = await _dbContext.GetConnectionAsync();
        var user = await conn.QueryFirstOrDefaultAsync<User>(
            $"SELECT {PublicColumns}, PasswordHash FROM Users WHERE Username = @Username",
            new { Username = name });

        if (user == null)
        {
            PasswordHasher.VerifyPassword(plainPassword, DummyHash);
            await LogFailedLoginAsync(name);
            return LoginResult.Failed(LoginFailureReason.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            PasswordHasher.VerifyPassword(plainPassword, user.PasswordHash);
            await LogFailedLoginAsync(name);
            return LoginResult.Failed(LoginFailureReason.Inactive);
        }

        if (user.LockoutUntil is DateTime lockedUntil && lockedUntil > UtcNow)
            return LoginResult.Failed(LoginFailureReason.LockedOut, lockedUntil - UtcNow);

        if (!PasswordHasher.VerifyPassword(plainPassword, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(conn, user);
            return LoginResult.Failed(LoginFailureReason.InvalidCredentials);
        }

        var storedHash = user.PasswordHash;
        user.PasswordHash = string.Empty;
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;

        if (PasswordHasher.NeedsRehash(storedHash))
        {
            await conn.ExecuteAsync(
                "UPDATE Users SET PasswordHash = @Hash, FailedLoginCount = 0, LockoutUntil = NULL WHERE Id = @Id",
                new { Id = user.Id, Hash = PasswordHasher.HashPassword(plainPassword) });
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE Users SET FailedLoginCount = 0, LockoutUntil = NULL WHERE Id = @Id",
                new { Id = user.Id });
        }

        return LoginResult.Success(user);
    }

    private async Task RegisterFailedAttemptAsync(SqliteConnection conn, User user)
    {
        var failedAttempts = user.FailedLoginCount + 1;
        if (failedAttempts >= MaxFailedAttempts)
        {
            await conn.ExecuteAsync(
                "UPDATE Users SET FailedLoginCount = 0, LockoutUntil = @LockoutUntil WHERE Id = @Id",
                new { Id = user.Id, LockoutUntil = UtcNow + LockoutDuration });
        }
        else
        {
            await conn.ExecuteAsync(
                "UPDATE Users SET FailedLoginCount = @Count WHERE Id = @Id",
                new { Id = user.Id, Count = failedAttempts });
        }

        await LogFailedLoginAsync(user.Username);
    }

    private async Task LogFailedLoginAsync(string username)
    {
        try
        {
            await _logService.AddLogAsync($"فشل تسجيل الدخول للمستخدم: {username}", "warning");
        }
        catch (Exception ex)
        {
            // Auditing must never block the login path, but a silent failure
            // would hide a broken audit trail.
            System.Diagnostics.Trace.TraceWarning(
                "Failed to write login audit entry for {0}: {1}", username, ex);
        }
    }

    public async Task<bool> UserExistsAsync(string username, string? excludeId = null)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var sql = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
        if (!string.IsNullOrEmpty(excludeId))
            sql += " AND Id != @Id";
        var count = await conn.ExecuteScalarAsync<int>(sql, new { Username = username, Id = excludeId });
        return count > 0;
    }
}

public static class PasswordHasher
{
    public const int CurrentIterations = 600_000;
    private const int LegacyIterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, KeySize);
        return $"v2${CurrentIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            if (storedHash.StartsWith("v2$", StringComparison.Ordinal))
            {
                var parts = storedHash.Split('$');
                if (parts.Length != 4)
                    return false;
                if (!int.TryParse(parts[1], out var iterations) || iterations < 1 || iterations > 5_000_000)
                    return false;

                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != SaltSize || expected.Length != KeySize)
                    return false;

                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            // Legacy deployment format: base64(salt | hash), 100k iterations.
            var fullHash = Convert.FromBase64String(storedHash);
            if (fullHash.Length != SaltSize + KeySize)
                return false;

            var legacySalt = fullHash[..SaltSize];
            var legacyExpected = fullHash[SaltSize..];
            var legacyActual = Rfc2898DeriveBytes.Pbkdf2(
                password, legacySalt, LegacyIterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(legacyActual, legacyExpected);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Password hash verification failed: {0}", ex);
            return false;
        }
    }

    public static bool NeedsRehash(string storedHash)
    {
        if (!storedHash.StartsWith("v2$", StringComparison.Ordinal))
            return true;
        var parts = storedHash.Split('$');
        return parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < CurrentIterations;
    }
}
