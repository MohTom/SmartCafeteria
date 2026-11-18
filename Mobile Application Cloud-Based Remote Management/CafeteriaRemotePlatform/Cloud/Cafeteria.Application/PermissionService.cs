using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed class PermissionService
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public PermissionService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TenantMembership> GetMembershipAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await _db.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.IsActive,
                cancellationToken);

        if (membership is null)
            throw AppException.Forbidden("You are not a member of this company.");

        return membership;
    }

    public async Task<TenantMembership> EnsureRoleAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken,
        params TenantRole[] roles)
    {
        var membership = await GetMembershipAsync(userId, tenantId, cancellationToken);
        if (!roles.Contains(membership.Role))
            throw AppException.Forbidden("Your company role does not allow this action.");
        return membership;
    }

    public async Task<Installation> GetInstallationAsync(
        Guid tenantId,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var installation = await _db.Installations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.Id == installationId && i.TenantId == tenantId && i.IsActive,
                cancellationToken);

        if (installation is null)
            throw AppException.NotFound("Installation was not found.");

        return installation;
    }

    public async Task<InstallationAccessDto> GetAccessAsync(
        Guid userId,
        Guid tenantId,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(userId, tenantId, cancellationToken);

        if (membership.Role is TenantRole.Owner or TenantRole.Admin)
            return new InstallationAccessDto(true, true, true);

        var access = await _db.InstallationAccess
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.InstallationId == installationId && a.UserId == userId,
                cancellationToken);

        return access is null
            ? new InstallationAccessDto(false, false, false)
            : new InstallationAccessDto(
                access.CanViewReports,
                access.CanManageUsers,
                access.CanAdjustInventory);
    }

    public async Task EnsureInstallationPermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid installationId,
        string permission,
        CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(userId, tenantId, cancellationToken);
        if (!RolePermissions.Allows(membership.Role, permission))
            throw AppException.Forbidden("Your company role does not allow this action.");

        if (membership.Role is TenantRole.Owner or TenantRole.Admin)
            return;

        var access = await _db.InstallationAccess
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.InstallationId == installationId && a.UserId == userId,
                cancellationToken);

        if (access is null)
            throw AppException.Forbidden("You do not have access to this installation.");

        var allowed = permission switch
        {
            Permissions.ReportsRead => access.CanViewReports,
            Permissions.UsersManage => access.CanManageUsers,
            Permissions.InventoryAdjust => access.CanAdjustInventory,
            Permissions.InstallationRead => true,
            _ => false
        };

        if (!allowed)
            throw AppException.Forbidden("You do not have the required installation permission.");
    }

    public async Task<IReadOnlySet<Guid>> GetAccessibleInstallationIdsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(userId, tenantId, cancellationToken);

        if (membership.Role is TenantRole.Owner or TenantRole.Admin)
        {
            return (await _db.Installations
                    .AsNoTracking()
                    .Where(i => i.TenantId == tenantId && i.IsActive)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        return (await _db.InstallationAccess
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.InstallationId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    public bool IsOnline(Installation installation) =>
        installation.LastSeenAt.HasValue &&
        installation.LastSeenAt.Value >= _clock.UtcNow - OnlineWindow;
}
