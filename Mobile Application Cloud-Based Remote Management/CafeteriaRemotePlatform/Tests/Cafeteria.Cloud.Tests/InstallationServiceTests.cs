using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class InstallationServiceTests
{
    private static InstallationService CreateService(TestDatabase database) =>
        new(
            database.Db,
            database.Permissions(),
            database.Tokens,
            database.Clock,
            database.Realtime);

    [Fact]
    public async Task CreateClaimCodeAsync_AllowsManagerAndRejectsViewer()
    {
        using var database = new TestDatabase();
        var (tenant, manager, _) = await database.SeedTenantAsync(role: TenantRole.Manager);
        var service = CreateService(database);

        var code = await service.CreateClaimCodeAsync(manager.Id, tenant.Id, CancellationToken.None);
        Assert.Equal(9, code.Code.Length);
        Assert.True(code.ExpiresAt > database.Clock.UtcNow);

        var (viewerTenant, viewer, _) = await database.SeedTenantAsync(
            tenantName: "Viewer Cafe",
            role: TenantRole.Viewer,
            email: "viewer@example.com");

        var forbidden = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateClaimCodeAsync(viewer.Id, viewerTenant.Id, CancellationToken.None));
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task ClaimAsync_CreatesInstallationAndManageAccess()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);
        var claimCode = await service.CreateClaimCodeAsync(owner.Id, tenant.Id, CancellationToken.None);

        var response = await service.ClaimAsync(
            new ClaimInstallationRequest(claimCode.Code, "Front Till", "TILL-01", "1.0.0"),
            CancellationToken.None);

        Assert.Equal(tenant.Id, response.TenantId);
        Assert.Equal("Front Till", response.InstallationName);
        Assert.NotEmpty(response.InstallationSecret);
        var installation = await database.Db.Installations.SingleAsync();
        Assert.Equal(response.InstallationId, installation.Id);
        Assert.Equal(installation.Id, response.InstallationId);
        var access = await database.Db.InstallationAccess.SingleAsync();
        Assert.Equal(owner.Id, access.UserId);
        Assert.True(access.CanAdjustInventory);
        var claim = await database.Db.InstallationClaimCodes.SingleAsync();
        Assert.NotNull(claim.UsedAt);
        Assert.Equal(installation.Id, claim.CreatedInstallationId);
    }

    [Fact]
    public async Task ClaimAsync_RejectsInvalidExpiredAndReusedCodes()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);

        var missing = await Assert.ThrowsAsync<AppException>(() =>
            service.ClaimAsync(
                new ClaimInstallationRequest("", "Till", "PC", "1.0.0"),
                CancellationToken.None));
        Assert.Equal("invalid_claim_code", missing.Code);

        var expiredCode = await service.CreateClaimCodeAsync(owner.Id, tenant.Id, CancellationToken.None);
        database.Clock.Advance(TimeSpan.FromMinutes(11));
        var expired = await Assert.ThrowsAsync<AppException>(() =>
            service.ClaimAsync(
                new ClaimInstallationRequest(expiredCode.Code, "Till", "PC", "1.0.0"),
                CancellationToken.None));
        Assert.Equal("invalid_claim_code", expired.Code);

        database.Clock.Advance(TimeSpan.FromMinutes(-11));
        var reuse = await service.CreateClaimCodeAsync(owner.Id, tenant.Id, CancellationToken.None);
        await service.ClaimAsync(
            new ClaimInstallationRequest(reuse.Code, "Till 1", "PC1", "1.0.0"),
            CancellationToken.None);

        var reused = await Assert.ThrowsAsync<AppException>(() =>
            service.ClaimAsync(
                new ClaimInstallationRequest(reuse.Code, "Till 2", "PC2", "1.0.0"),
                CancellationToken.None));
        Assert.Equal("invalid_claim_code", reused.Code);
    }

    [Fact]
    public async Task CreateTokenAsync_ReturnsTokenForMatchingSecret()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);
        var claimCode = await service.CreateClaimCodeAsync(owner.Id, tenant.Id, CancellationToken.None);
        var claimed = await service.ClaimAsync(
            new ClaimInstallationRequest(claimCode.Code, "Till", "PC", "1.0.0"),
            CancellationToken.None);

        var token = await service.CreateTokenAsync(
            new InstallationTokenRequest(claimed.InstallationId, claimed.InstallationSecret),
            CancellationToken.None);

        Assert.StartsWith("installation-access:", token.AccessToken);
        Assert.True(token.ExpiresAt > database.Clock.UtcNow);

        var invalid = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateTokenAsync(
                new InstallationTokenRequest(claimed.InstallationId, "wrong"),
                CancellationToken.None));
        Assert.Equal(401, invalid.StatusCode);
    }

    [Fact]
    public async Task HeartbeatAsync_UpdatesSeenStateAndPublishesStatus()
    {
        using var database = new TestDatabase();
        var (tenant, _, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id, online: false);
        var service = CreateService(database);

        await service.HeartbeatAsync(
            installation.Id,
            new HeartbeatRequest("2.0.0"),
            CancellationToken.None);

        var updated = await database.Db.Installations.SingleAsync();
        Assert.Equal("2.0.0", updated.AppVersion);
        Assert.Equal(database.Clock.UtcNow, updated.LastSeenAt);
        Assert.Single(database.Realtime.InstallationStatuses);
        Assert.True(database.Realtime.InstallationStatuses[0].IsOnline);
    }

    [Fact]
    public async Task UpdateAccessAsync_CreatesAndUpdatesInstallationAccess()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var member = new User
        {
            Email = "member@example.com",
            NormalizedEmail = "MEMBER@EXAMPLE.COM",
            DisplayName = "Member"
        };
        database.Db.Users.Add(member);
        database.Db.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant.Id,
            UserId = member.Id,
            Role = TenantRole.Viewer
        });
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        var created = await service.UpdateAccessAsync(
            owner.Id,
            tenant.Id,
            installation.Id,
            new UpdateInstallationAccessRequest(member.Id, true, false, true),
            CancellationToken.None);

        Assert.True(created.Access.CanViewReports);
        Assert.True(created.Access.CanAdjustInventory);
        var stored = await database.Db.InstallationAccess.SingleAsync();
        Assert.Equal(AccessLevel.Manage, stored.AccessLevel);

        var updated = await service.UpdateAccessAsync(
            owner.Id,
            tenant.Id,
            installation.Id,
            new UpdateInstallationAccessRequest(member.Id, false, false, false),
            CancellationToken.None);

        Assert.False(updated.Access.CanViewReports);
        Assert.Equal(AccessLevel.View, (await database.Db.InstallationAccess.SingleAsync()).AccessLevel);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyAccessibleInstallations()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var visible = await database.SeedInstallationAsync(tenant.Id, name: "Visible");
        var hidden = await database.SeedInstallationAsync(tenant.Id, name: "Hidden");
        var viewer = new User
        {
            Email = "viewer@example.com",
            NormalizedEmail = "VIEWER@EXAMPLE.COM",
            DisplayName = "Viewer"
        };
        database.Db.Users.Add(viewer);
        database.Db.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant.Id,
            UserId = viewer.Id,
            Role = TenantRole.Viewer
        });
        database.Db.InstallationAccess.Add(new InstallationAccess
        {
            InstallationId = visible.Id,
            UserId = viewer.Id,
            AccessLevel = AccessLevel.View,
            CanViewReports = true
        });
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        var adminList = await service.GetForUserAsync(owner.Id, tenant.Id, CancellationToken.None);
        Assert.Equal(2, adminList.Count);

        var viewerList = await service.GetForUserAsync(viewer.Id, tenant.Id, CancellationToken.None);
        Assert.Single(viewerList);
        Assert.Equal(visible.Id, viewerList[0].Id);
        Assert.True(viewerList[0].Access.CanViewReports);

        var forbidden = await Assert.ThrowsAsync<AppException>(() =>
            service.GetForUserAsync(
                viewer.Id,
                tenant.Id,
                hidden.Id,
                CancellationToken.None));
        Assert.Equal(403, forbidden.StatusCode);
    }
}
