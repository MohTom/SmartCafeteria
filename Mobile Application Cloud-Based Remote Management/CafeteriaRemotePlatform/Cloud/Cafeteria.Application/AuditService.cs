using Cafeteria.Domain;

namespace Cafeteria.Application;

public sealed class AuditService
{
    private readonly IAppDbContext _db;

    public AuditService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        Guid? tenantId = null,
        Guid? userId = null,
        Guid? installationId = null,
        string? entityId = null,
        string? detailsJson = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            InstallationId = installationId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = detailsJson,
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
