using Cafeteria.Application;
using Cafeteria.Domain;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task EnsureRoleAsync_RejectsNonMembersAndWrongRoles()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var permissions = database.Permissions();

        var nonMember = await Assert.ThrowsAsync<AppException>(() =>
            permissions.EnsureRoleAsync(Guid.NewGuid(), tenant.Id, CancellationToken.None, TenantRole.Owner));
        Assert.Equal(403, nonMember.StatusCode);

        var wrongRole = await Assert.ThrowsAsync<AppException>(() =>
            permissions.EnsureRoleAsync(owner.Id, tenant.Id, CancellationToken.None, TenantRole.Viewer));
        Assert.Equal(403, wrongRole.StatusCode);
    }

    [Fact]
    public async Task EnsureInstallationPermissionAsync_EnforcesRoleAndAccess()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
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
        await database.Db.SaveChangesAsync();
        var permissions = database.Permissions();

        await permissions.EnsureInstallationPermissionAsync(
            owner.Id,
            tenant.Id,
            installation.Id,
            Permissions.UsersManage,
            CancellationToken.None);

        var noAccess = await Assert.ThrowsAsync<AppException>(() =>
            permissions.EnsureInstallationPermissionAsync(
                viewer.Id,
                tenant.Id,
                installation.Id,
                Permissions.InstallationRead,
                CancellationToken.None));
        Assert.Equal(403, noAccess.StatusCode);

        database.Db.InstallationAccess.Add(new InstallationAccess
        {
            InstallationId = installation.Id,
            UserId = viewer.Id,
            CanViewReports = true,
            CanManageUsers = false,
            CanAdjustInventory = false
        });
        await database.Db.SaveChangesAsync();

        await permissions.EnsureInstallationPermissionAsync(
            viewer.Id,
            tenant.Id,
            installation.Id,
            Permissions.ReportsRead,
            CancellationToken.None);

        var users = await Assert.ThrowsAsync<AppException>(() =>
            permissions.EnsureInstallationPermissionAsync(
                viewer.Id,
                tenant.Id,
                installation.Id,
                Permissions.UsersManage,
                CancellationToken.None));
        Assert.Equal(403, users.StatusCode);
    }

    [Fact]
    public async Task GetAccessAsync_ReturnsAdminBypassAndStoredAccess()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
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
        await database.Db.SaveChangesAsync();
        var permissions = database.Permissions();

        var adminAccess = await permissions.GetAccessAsync(owner.Id, tenant.Id, installation.Id, CancellationToken.None);
        Assert.True(adminAccess.CanManageUsers);

        var noAccess = await permissions.GetAccessAsync(viewer.Id, tenant.Id, installation.Id, CancellationToken.None);
        Assert.False(noAccess.CanViewReports);

        database.Db.InstallationAccess.Add(new InstallationAccess
        {
            InstallationId = installation.Id,
            UserId = viewer.Id,
            CanViewReports = true
        });
        await database.Db.SaveChangesAsync();

        var stored = await permissions.GetAccessAsync(viewer.Id, tenant.Id, installation.Id, CancellationToken.None);
        Assert.True(stored.CanViewReports);
        Assert.False(stored.CanManageUsers);
    }

    [Fact]
    public async Task GetAccessibleInstallationIdsAsync_ReturnsAdminAndGrantedOnly()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var first = await database.SeedInstallationAsync(tenant.Id, name: "First");
        var second = await database.SeedInstallationAsync(tenant.Id, name: "Second");
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
            InstallationId = first.Id,
            UserId = viewer.Id
        });
        await database.Db.SaveChangesAsync();
        var permissions = database.Permissions();

        var adminIds = await permissions.GetAccessibleInstallationIdsAsync(owner.Id, tenant.Id, CancellationToken.None);
        Assert.Equal(2, adminIds.Count);

        var viewerIds = await permissions.GetAccessibleInstallationIdsAsync(viewer.Id, tenant.Id, CancellationToken.None);
        Assert.Single(viewerIds);
        Assert.Contains(first.Id, viewerIds);
        Assert.DoesNotContain(second.Id, viewerIds);
    }

    [Fact]
    public void IsOnline_UsesSixtySecondWindow()
    {
        using var database = new TestDatabase();
        var permissions = database.Permissions();
        var installation = new Installation
        {
            LastSeenAt = database.Clock.UtcNow.AddSeconds(-30)
        };

        Assert.True(permissions.IsOnline(installation));

        database.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False(permissions.IsOnline(installation));

        installation.LastSeenAt = null;
        Assert.False(permissions.IsOnline(installation));
    }
}
