using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class CommandServiceTests
{
    private static CommandService CreateService(TestDatabase database) =>
        new(
            database.Db,
            database.Permissions(),
            database.Clock,
            database.Realtime,
            database.Pusher);

    [Fact]
    public async Task CreateAsync_RejectsUnknownTypeAndInvalidIdempotencyKey()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var unknown = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "sql.execute", "{}", "key-1"),
                CancellationToken.None));
        Assert.Equal("unsupported_command", unknown.Code);

        var badKey = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "users.list", "{}", ""),
                CancellationToken.None));
        Assert.Equal("invalid_idempotency_key", badKey.Code);
    }

    [Fact]
    public async Task CreateAsync_RejectsOnlineOnlyCommandsWhenInstallationOffline()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id, online: false);
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "users.lock", """{"userId":"u1"}""", "key-1"),
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("installation_offline", exception.Code);
        Assert.Empty(database.Db.RemoteCommands);
    }

    [Fact]
    public async Task CreateAsync_PushesOnlineCommandsAndMarksDispatched()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.lock", """{"userId":"u1"}""", "key-1"),
            CancellationToken.None);

        Assert.Equal("Dispatched", command.Status);
        Assert.Single(database.Pusher.Pushes);
        Assert.Single(database.Realtime.CommandCreated);
        var stored = await database.Db.RemoteCommands.SingleAsync();
        Assert.NotNull(stored.DispatchedAt);
    }

    [Fact]
    public async Task CreateAsync_QueuesInventoryAdjustmentWhenOffline()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id, online: false);
        var service = CreateService(database);

        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(
                installation.Id,
                "inventory.adjust",
                """{"productId":"p1","delta":-3}""",
                "key-1"),
            CancellationToken.None);

        Assert.Equal("Pending", command.Status);
        Assert.Empty(database.Pusher.Pushes);

        var pending = await service.GetPendingForInstallationAsync(installation.Id, CancellationToken.None);
        Assert.Single(pending);
        Assert.Equal(command.Id, pending[0].Id);
    }

    [Fact]
    public async Task CreateAsync_ValidatesCommandPayloads()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var invalidJson = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "users.lock", "{", "key-1"),
                CancellationToken.None));
        Assert.Equal("invalid_payload", invalidJson.Code);

        var notObject = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "users.lock", "[]", "key-2"),
                CancellationToken.None));
        Assert.Equal("invalid_payload", notObject.Code);

        var missingUserId = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(installation.Id, "users.lock", "{}", "key-3"),
                CancellationToken.None));
        Assert.Equal("invalid_payload", missingUserId.Code);

        var zeroDelta = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(
                owner.Id,
                tenant.Id,
                new CreateCommandRequest(
                    installation.Id,
                    "inventory.adjust",
                    """{"productId":"p1","delta":0}""",
                    "key-4"),
                CancellationToken.None));
        Assert.Equal("invalid_payload", zeroDelta.Code);
    }

    [Fact]
    public async Task CreateAsync_ReturnsExistingCommandForIdempotencyKey()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var first = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "same-key"),
            CancellationToken.None);
        var second = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "same-key"),
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await database.Db.RemoteCommands.ToListAsync());
    }

    [Fact]
    public async Task AcknowledgeAndCompleteAsync_AdvanceCommandLifecycle()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);
        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "key-1"),
            CancellationToken.None);

        var acknowledged = await service.AcknowledgeAsync(
            installation.Id,
            command.Id,
            CancellationToken.None);
        Assert.Equal("Executing", acknowledged.Status);
        Assert.NotNull(acknowledged.StartedAt);

        var duplicateAck = await service.AcknowledgeAsync(
            installation.Id,
            command.Id,
            CancellationToken.None);
        Assert.Equal("Executing", duplicateAck.Status);
        Assert.NotNull(duplicateAck.StartedAt);

        var completed = await service.CompleteAsync(
            installation.Id,
            command.Id,
            new CompleteCommandRequest(true, """{"ok":true}""", null),
            CancellationToken.None);
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Single(database.Realtime.CommandCompleted);

        var duplicateComplete = await service.CompleteAsync(
            installation.Id,
            command.Id,
            new CompleteCommandRequest(false, null, "ignored"),
            CancellationToken.None);
        Assert.Equal("Completed", duplicateComplete.Status);
    }

    [Fact]
    public async Task CompleteAsync_RecordsFailure()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);
        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "key-1"),
            CancellationToken.None);

        var completed = await service.CompleteAsync(
            installation.Id,
            command.Id,
            new CompleteCommandRequest(false, null, "desktop failure"),
            CancellationToken.None);

        Assert.Equal("Failed", completed.Status);
        Assert.Equal("desktop failure", completed.Error);
    }

    [Fact]
    public async Task CancelAsync_OnlyCancelsPendingOrDispatched()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);
        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "key-1"),
            CancellationToken.None);

        var cancelled = await service.CancelAsync(owner.Id, tenant.Id, command.Id, CancellationToken.None);
        Assert.Equal("Cancelled", cancelled.Status);

        var repeat = await Assert.ThrowsAsync<AppException>(() =>
            service.CancelAsync(owner.Id, tenant.Id, command.Id, CancellationToken.None));
        Assert.Equal(409, repeat.StatusCode);
    }

    [Fact]
    public async Task ExpireAsync_ExpiresPendingAndDispatchedCommands()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id, online: false);
        var service = CreateService(database);
        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(
                installation.Id,
                "inventory.adjust",
                """{"productId":"p1","delta":1}""",
                "key-1"),
            CancellationToken.None);

        database.Clock.Advance(TimeSpan.FromMinutes(31));
        var count = await service.ExpireAsync(CancellationToken.None);

        Assert.Equal(1, count);
        var stored = await database.Db.RemoteCommands.SingleAsync();
        Assert.Equal(CommandStatus.Expired, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.Equal(command.Id, stored.Id);
    }

    [Fact]
    public async Task GetDispatchableAsync_OnlyReturnsOnlineInstallationCommands()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var online = await database.SeedInstallationAsync(tenant.Id, online: true, name: "Online");
        var offline = await database.SeedInstallationAsync(tenant.Id, online: false, name: "Offline");
        var service = CreateService(database);

        database.Db.RemoteCommands.AddRange(
            new RemoteCommand
            {
                TenantId = tenant.Id,
                InstallationId = online.Id,
                RequestedByUserId = owner.Id,
                Type = "inventory.adjust",
                PayloadJson = """{"productId":"p1","delta":1}""",
                RequiredPermission = Permissions.InventoryAdjust,
                IdempotencyKey = "online-1",
                ExpiresAt = database.Clock.UtcNow.AddMinutes(10)
            },
            new RemoteCommand
            {
                TenantId = tenant.Id,
                InstallationId = offline.Id,
                RequestedByUserId = owner.Id,
                Type = "inventory.adjust",
                PayloadJson = """{"productId":"p1","delta":1}""",
                RequiredPermission = Permissions.InventoryAdjust,
                IdempotencyKey = "offline-1",
                ExpiresAt = database.Clock.UtcNow.AddMinutes(10)
            });
        await database.Db.SaveChangesAsync();

        var dispatchable = await service.GetDispatchableAsync(CancellationToken.None);

        Assert.Single(dispatchable);
        Assert.Equal(online.Id, dispatchable[0].InstallationId);
    }

    [Fact]
    public async Task GetAsync_RequiresInstallationPermission()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var outsider = new User
        {
            Email = "outsider@example.com",
            NormalizedEmail = "OUTSIDER@EXAMPLE.COM",
            DisplayName = "Outsider"
        };
        database.Db.Users.Add(outsider);
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);
        var command = await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(installation.Id, "users.list", "{}", "key-1"),
            CancellationToken.None);

        var forbidden = await Assert.ThrowsAsync<AppException>(() =>
            service.GetAsync(outsider.Id, tenant.Id, command.Id, CancellationToken.None));
        Assert.Equal(403, forbidden.StatusCode);

        var missing = await Assert.ThrowsAsync<AppException>(() =>
            service.GetAsync(owner.Id, tenant.Id, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task ListAsync_FiltersByInstallationAndSupportsAllAccessible()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var first = await database.SeedInstallationAsync(tenant.Id, name: "First");
        var second = await database.SeedInstallationAsync(tenant.Id, name: "Second");
        var service = CreateService(database);
        await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(first.Id, "users.list", "{}", "key-1"),
            CancellationToken.None);
        await service.CreateAsync(
            owner.Id,
            tenant.Id,
            new CreateCommandRequest(second.Id, "users.list", "{}", "key-2"),
            CancellationToken.None);

        var filtered = await service.ListAsync(owner.Id, tenant.Id, first.Id, CancellationToken.None);
        Assert.Single(filtered);

        var all = await service.ListAsync(owner.Id, tenant.Id, null, CancellationToken.None);
        Assert.Equal(2, all.Count);
    }
}
