using Cafeteria.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsAuditEntry()
    {
        using var database = new TestDatabase();
        var service = new AuditService(database.Db);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.LogAsync(
            "infrastructure.settings.updated",
            "Tenant",
            tenantId,
            userId,
            entityId: "tenant-1",
            detailsJson: """{"field":"name"}""",
            ipAddress: "127.0.0.1");

        var entry = await database.Db.AuditLogs.SingleAsync();
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal("infrastructure.settings.updated", entry.Action);
        Assert.Equal("Tenant", entry.EntityType);
        Assert.Equal("tenant-1", entry.EntityId);
        Assert.Equal("127.0.0.1", entry.IpAddress);
    }
}
