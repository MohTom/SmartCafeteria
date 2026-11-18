using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Installation> Installations { get; }
    DbSet<InstallationAccess> InstallationAccess { get; }
    DbSet<InstallationClaimCode> InstallationClaimCodes { get; }
    DbSet<RemoteCommand> RemoteCommands { get; }
    DbSet<BusinessEvent> BusinessEvents { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
