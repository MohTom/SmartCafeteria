using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class TenantServiceTests
{
    private static TenantService CreateService(TestDatabase database) =>
        new(database.Db, database.Tokens, database.Clock, database.Email);

    [Fact]
    public async Task GetTenantsAsync_ReturnsActiveMembershipsOnly()
    {
        using var database = new TestDatabase();
        var (tenant, user, _) = await database.SeedTenantAsync();
        var inactiveTenant = new Tenant { Name = "Closed", Slug = "closed", IsActive = false };
        database.Db.Tenants.Add(inactiveTenant);
        database.Db.TenantMemberships.Add(new TenantMembership
        {
            Tenant = inactiveTenant,
            UserId = user.Id,
            Role = TenantRole.Viewer
        });
        await database.Db.SaveChangesAsync();

        var tenants = await CreateService(database).GetTenantsAsync(user.Id, CancellationToken.None);

        Assert.Single(tenants);
        Assert.Equal(tenant.Id, tenants[0].Id);
    }

    [Fact]
    public async Task GetMembersAsync_RequiresActiveMembership()
    {
        using var database = new TestDatabase();
        var (tenant, _, _) = await database.SeedTenantAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            CreateService(database).GetMembersAsync(Guid.NewGuid(), tenant.Id, CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task CreateInvitationAsync_CreatesCodeAndSendsEmail()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);

        var invitation = await service.CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            new InviteMemberRequest("manager@example.com", "Manager"),
            CancellationToken.None);

        Assert.Equal("Manager", invitation.Role);
        Assert.Equal(12, invitation.InviteCode.Length);
        Assert.Single(database.Email.Invitations);
        Assert.Equal("manager@example.com", database.Email.Invitations[0].Email);
        var stored = await database.Db.Invitations.SingleAsync();
        Assert.Equal(database.Tokens.HashToken(invitation.InviteCode), stored.TokenHash);
    }

    [Fact]
    public async Task CreateInvitationAsync_EnforcesRoleRules()
    {
        using var database = new TestDatabase();
        var (tenant, manager, _) = await database.SeedTenantAsync(role: TenantRole.Manager);
        var service = CreateService(database);

        var forbidden = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateInvitationAsync(
                manager.Id,
                tenant.Id,
                new InviteMemberRequest("viewer@example.com", "Viewer"),
                CancellationToken.None));
        Assert.Equal(403, forbidden.StatusCode);

        var (ownerTenant, owner, _) = await database.SeedTenantAsync(
            tenantName: "Other",
            role: TenantRole.Owner,
            email: "owner2@example.com");

        var invalidRole = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateInvitationAsync(
                owner.Id,
                ownerTenant.Id,
                new InviteMemberRequest("other@example.com", "Owner"),
                CancellationToken.None));
        Assert.Equal("invalid_role", invalidRole.Code);

        var adminByOwner = await service.CreateInvitationAsync(
            owner.Id,
            ownerTenant.Id,
            new InviteMemberRequest("admin@example.com", "Admin"),
            CancellationToken.None);
        Assert.Equal("Admin", adminByOwner.Role);

        var admin = new User
        {
            Email = "admin@example.com",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            DisplayName = "Admin"
        };
        database.Db.Users.Add(admin);
        database.Db.TenantMemberships.Add(new TenantMembership
        {
            TenantId = ownerTenant.Id,
            UserId = admin.Id,
            Role = TenantRole.Admin
        });
        await database.Db.SaveChangesAsync();

        var adminCannotInviteAdmin = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateInvitationAsync(
                admin.Id,
                ownerTenant.Id,
                new InviteMemberRequest("third@example.com", "Admin"),
                CancellationToken.None));
        Assert.Equal(403, adminCannotInviteAdmin.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationAsync_AddsMembershipForMatchingEmail()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);
        var invitation = await service.CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            new InviteMemberRequest("new@example.com", "Manager"),
            CancellationToken.None);
        var invitedUser = new User
        {
            Email = "new@example.com",
            NormalizedEmail = "NEW@EXAMPLE.COM",
            DisplayName = "New"
        };
        database.Db.Users.Add(invitedUser);
        await database.Db.SaveChangesAsync();

        var summary = await service.AcceptInvitationAsync(
            invitedUser.Id,
            new AcceptInvitationRequest(invitation.InviteCode),
            CancellationToken.None);

        Assert.Equal(tenant.Id, summary.Id);
        var membership = await database.Db.TenantMemberships
            .SingleAsync(m => m.UserId == invitedUser.Id);
        Assert.Equal(TenantRole.Manager, membership.Role);
        Assert.NotNull((await database.Db.Invitations.SingleAsync()).AcceptedAt);
    }

    [Fact]
    public async Task AcceptInvitationAsync_RejectsWrongEmailAndUsedCode()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);
        var invitation = await service.CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            new InviteMemberRequest("new@example.com", "Viewer"),
            CancellationToken.None);
        var wrongUser = await database.SeedTenantAsync(
            tenantName: "Other",
            email: "wrong@example.com");

        var wrongEmail = await Assert.ThrowsAsync<AppException>(() =>
            service.AcceptInvitationAsync(
                wrongUser.User.Id,
                new AcceptInvitationRequest(invitation.InviteCode),
                CancellationToken.None));
        Assert.Equal(403, wrongEmail.StatusCode);

        var invitedUser = new User
        {
            Email = "new@example.com",
            NormalizedEmail = "NEW@EXAMPLE.COM",
            DisplayName = "New"
        };
        database.Db.Users.Add(invitedUser);
        await database.Db.SaveChangesAsync();
        await service.AcceptInvitationAsync(
            invitedUser.Id,
            new AcceptInvitationRequest(invitation.InviteCode),
            CancellationToken.None);

        var reused = await Assert.ThrowsAsync<AppException>(() =>
            service.AcceptInvitationAsync(
                invitedUser.Id,
                new AcceptInvitationRequest(invitation.InviteCode),
                CancellationToken.None));
        Assert.Equal("invalid_invite_code", reused.Code);
    }
}
