using Cafeteria.Application;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafeteria.Cloud.Tests;

public sealed class EventServiceTests
{
    private static EventService CreateService(TestDatabase database) =>
        new(database.Db, database.Clock, database.Push, database.Realtime);

    [Fact]
    public async Task IngestAsync_AcceptsEventsAndCreatesNotificationsForManagers()
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
        var service = CreateService(database);

        var response = await service.IngestAsync(
            installation.Id,
            new EventBatchRequest(
            [
                new EventDto(
                    "event-1",
                    "order.created",
                    """{"code":"ORD-1","total":"12.50"}""",
                    database.Clock.UtcNow)
            ]),
            CancellationToken.None);

        Assert.Equal(1, response.Accepted);
        Assert.Equal(0, response.Duplicates);
        var notification = await database.Db.Notifications.SingleAsync();
        Assert.Equal(owner.Id, notification.UserId);
        Assert.Equal("New order", notification.Title);
        Assert.Single(database.Realtime.Notifications);
        Assert.Empty(database.Push.Sends);
    }

    [Fact]
    public async Task IngestAsync_DeduplicatesByEventId()
    {
        using var database = new TestDatabase();
        var (tenant, _, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var response = await service.IngestAsync(
            installation.Id,
            new EventBatchRequest(
            [
                new EventDto("event-1", "order.created", "{}", database.Clock.UtcNow),
                new EventDto("event-1", "order.created", "{}", database.Clock.UtcNow)
            ]),
            CancellationToken.None);

        Assert.Equal(1, response.Accepted);
        Assert.Equal(1, response.Duplicates);
        Assert.Single(await database.Db.BusinessEvents.ToListAsync());
    }

    [Fact]
    public async Task IngestAsync_RejectsInvalidEventsAndBatches()
    {
        using var database = new TestDatabase();
        var (tenant, _, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);

        var invalidId = await Assert.ThrowsAsync<AppException>(() =>
            service.IngestAsync(
                installation.Id,
                new EventBatchRequest([new EventDto("", "order.created", "{}", database.Clock.UtcNow)]),
                CancellationToken.None));
        Assert.Equal("invalid_event", invalidId.Code);

        var invalidJson = await Assert.ThrowsAsync<AppException>(() =>
            service.IngestAsync(
                installation.Id,
                new EventBatchRequest([new EventDto("event-1", "order.created", "{", database.Clock.UtcNow)]),
                CancellationToken.None));
        Assert.Equal("invalid_payload", invalidJson.Code);

        var tooLarge = await Assert.ThrowsAsync<AppException>(() =>
            service.IngestAsync(
                installation.Id,
                new EventBatchRequest(
                    Enumerable.Range(0, 101)
                        .Select(i => new EventDto($"event-{i}", "order.created", "{}", database.Clock.UtcNow))
                        .ToList()),
                CancellationToken.None));
        Assert.Equal("batch_too_large", tooLarge.Code);

        var empty = await service.IngestAsync(
            installation.Id,
            new EventBatchRequest([]),
            CancellationToken.None);
        Assert.Equal(0, empty.Accepted);
    }

    [Fact]
    public async Task IngestAsync_SendsPushToRegisteredDevices()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        database.Db.DeviceTokens.Add(new DeviceToken
        {
            UserId = owner.Id,
            ExpoPushToken = "ExponentPushToken[test]",
            Platform = "android"
        });
        await database.Db.SaveChangesAsync();
        var service = CreateService(database);

        await service.IngestAsync(
            installation.Id,
            new EventBatchRequest(
            [
                new EventDto(
                    "event-1",
                    "system.alert",
                    """{"message":"Printer offline"}""",
                    database.Clock.UtcNow)
            ]),
            CancellationToken.None);

        Assert.Single(database.Push.Sends);
        Assert.Equal("System alert", database.Push.Sends[0].Title);
        Assert.Contains("ExponentPushToken[test]", database.Push.Sends[0].Tokens);
    }

    [Fact]
    public async Task NotificationEndpoints_ReadAndCount()
    {
        using var database = new TestDatabase();
        var (tenant, owner, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        var service = CreateService(database);
        await service.IngestAsync(
            installation.Id,
            new EventBatchRequest(
            [
                new EventDto("event-1", "order.created", "{}", database.Clock.UtcNow),
                new EventDto("event-2", "order.cancelled", "{}", database.Clock.UtcNow)
            ]),
            CancellationToken.None);

        var unread = await service.GetNotificationsAsync(
            owner.Id,
            tenant.Id,
            unreadOnly: false,
            CancellationToken.None);
        Assert.Equal(2, unread.Count);
        Assert.Equal(2, await service.GetUnreadCountAsync(owner.Id, tenant.Id, CancellationToken.None));

        await service.MarkReadAsync(owner.Id, unread[0].Id, CancellationToken.None);

        Assert.Single(await service.GetNotificationsAsync(
            owner.Id,
            tenant.Id,
            unreadOnly: true,
            CancellationToken.None));
        Assert.Equal(1, await service.GetUnreadCountAsync(owner.Id, tenant.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterDeviceAsync_ValidatesAndUpdatesTokens()
    {
        using var database = new TestDatabase();
        var (_, owner, _) = await database.SeedTenantAsync();
        var service = CreateService(database);

        var invalid = await Assert.ThrowsAsync<AppException>(() =>
            service.RegisterDeviceAsync(
                owner.Id,
                new RegisterDeviceRequest("not-a-token", "ios"),
                CancellationToken.None));
        Assert.Equal("invalid_push_token", invalid.Code);

        await service.RegisterDeviceAsync(
            owner.Id,
            new RegisterDeviceRequest("ExponentPushToken[one]", "ios"),
            CancellationToken.None);
        await service.RegisterDeviceAsync(
            owner.Id,
            new RegisterDeviceRequest("ExponentPushToken[one]", "android"),
            CancellationToken.None);

        var token = await database.Db.DeviceTokens.SingleAsync();
        Assert.Equal("android", token.Platform);
        Assert.True(token.IsActive);
    }

    [Fact]
    public async Task IngestAsync_RejectsInactiveInstallation()
    {
        using var database = new TestDatabase();
        var (tenant, _, _) = await database.SeedTenantAsync();
        var installation = await database.SeedInstallationAsync(tenant.Id);
        installation.IsActive = false;
        await database.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            CreateService(database).IngestAsync(
                installation.Id,
                new EventBatchRequest([new EventDto("event-1", "order.created", "{}", database.Clock.UtcNow)]),
                CancellationToken.None));

        Assert.Equal(404, exception.StatusCode);
    }
}
