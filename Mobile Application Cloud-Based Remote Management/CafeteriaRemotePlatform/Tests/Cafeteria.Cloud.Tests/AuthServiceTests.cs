using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterCompanyAsync_CreatesOwnerTenantAndSession()
    {
        using var database = new TestDatabase();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var response = await service.RegisterCompanyAsync(
            new RegisterCompanyRequest("Corner Cafe", "owner@example.com", "Owner", "password123"),
            CancellationToken.None);

        Assert.Equal("owner@example.com", response.User.Email);
        Assert.Single(response.User.Tenants);
        Assert.Equal("Owner", response.User.Tenants[0].Role);
        Assert.StartsWith("access:", response.Tokens.AccessToken);
        Assert.StartsWith("hash:refresh:", await database.Db.RefreshTokens
            .Select(t => t.TokenHash)
            .SingleAsync());
    }

    [Fact]
    public async Task RegisterCompanyAsync_RejectsDuplicateEmail()
    {
        using var database = new TestDatabase();
        await database.SeedTenantAsync(email: "owner@example.com");
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterCompanyAsync(
                new RegisterCompanyRequest("Second Cafe", "OWNER@example.com", "Owner", "password123"),
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("email_exists", exception.Code);
    }

    [Fact]
    public async Task RegisterCompanyAsync_ValidatesInput()
    {
        using var database = new TestDatabase();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var badEmail = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterCompanyAsync(
                new RegisterCompanyRequest("Cafe", "not-an-email", "Owner", "password123"),
                CancellationToken.None));
        Assert.Equal("invalid_email", badEmail.Code);

        var weakPassword = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterCompanyAsync(
                new RegisterCompanyRequest("Cafe", "owner@example.com", "Owner", "short"),
                CancellationToken.None));
        Assert.Equal("weak_password", weakPassword.Code);

        var missingCompany = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterCompanyAsync(
                new RegisterCompanyRequest("", "owner@example.com", "Owner", "password123"),
                CancellationToken.None));
        Assert.Equal("invalid_company_name", missingCompany.Code);
    }

    [Fact]
    public async Task LoginAsync_ReturnsSessionForValidCredentials()
    {
        using var database = new TestDatabase();
        var (_, user, _) = await database.SeedTenantAsync();
        user.PasswordHash = database.Passwords.Hash("password123");
        await database.Db.SaveChangesAsync();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var response = await service.LoginAsync(
            new LoginRequest("OWNER@EXAMPLE.COM", "password123"),
            CancellationToken.None);

        Assert.Equal(user.Id, response.User.Id);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task LoginAsync_RejectsWrongPasswordAndInactiveUser()
    {
        using var database = new TestDatabase();
        var (_, user, _) = await database.SeedTenantAsync();
        user.PasswordHash = database.Passwords.Hash("password123");
        user.IsActive = false;
        await database.Db.SaveChangesAsync();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.LoginAsync(
                new LoginRequest("owner@example.com", "wrong-password"),
                CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_RotatesTokenAndRevokesOldOne()
    {
        using var database = new TestDatabase();
        var (_, user, _) = await database.SeedTenantAsync();
        user.PasswordHash = database.Passwords.Hash("password123");
        await database.Db.SaveChangesAsync();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);
        var login = await service.LoginAsync(
            new LoginRequest("owner@example.com", "password123"),
            CancellationToken.None);

        var refreshed = await service.RefreshAsync(
            new RefreshRequest(login.Tokens.RefreshToken),
            CancellationToken.None);

        Assert.NotEqual(login.Tokens.RefreshToken, refreshed.Tokens.RefreshToken);
        var oldToken = await database.Db.RefreshTokens
            .SingleAsync(t => t.TokenHash == database.Tokens.HashToken(login.Tokens.RefreshToken));
        Assert.NotNull(oldToken.RevokedAt);
        Assert.NotNull(oldToken.ReplacedByTokenHash);
    }

    [Fact]
    public async Task RefreshAsync_RejectsMissingAndExpiredTokens()
    {
        using var database = new TestDatabase();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var missing = await Assert.ThrowsAsync<AppException>(() =>
            service.RefreshAsync(new RefreshRequest(""), CancellationToken.None));
        Assert.Equal(401, missing.StatusCode);

        database.Db.RefreshTokens.Add(new Domain.RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = database.Tokens.HashToken("expired"),
            ExpiresAt = database.Clock.UtcNow.AddSeconds(-1)
        });
        await database.Db.SaveChangesAsync();

        var expired = await Assert.ThrowsAsync<AppException>(() =>
            service.RefreshAsync(new RefreshRequest("expired"), CancellationToken.None));
        Assert.Equal(401, expired.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_RevokesActiveToken()
    {
        using var database = new TestDatabase();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);
        var registered = await service.RegisterCompanyAsync(
            new RegisterCompanyRequest("Cafe", "owner@example.com", "Owner", "password123"),
            CancellationToken.None);

        await service.LogoutAsync(
            new LogoutRequest(registered.Tokens.RefreshToken),
            CancellationToken.None);

        var token = await database.Db.RefreshTokens
            .SingleAsync(t => t.TokenHash == database.Tokens.HashToken(registered.Tokens.RefreshToken));
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsActiveTenantsOnly()
    {
        using var database = new TestDatabase();
        var (tenant, user, _) = await database.SeedTenantAsync();
        tenant.IsActive = false;
        await database.Db.SaveChangesAsync();
        var service = new AuthService(
            database.Db,
            database.Passwords,
            database.Tokens,
            database.Clock);

        var profile = await service.GetProfileAsync(user.Id, CancellationToken.None);

        Assert.Empty(profile.Tenants);
    }
}
