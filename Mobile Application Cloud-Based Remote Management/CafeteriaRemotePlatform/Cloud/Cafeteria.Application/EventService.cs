using System.Text.Json;
using Cafeteria.Contracts;
using Cafeteria.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Application;

public sealed class EventService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IPushSender _push;
    private readonly IRealtimePublisher _realtime;

    public EventService(
        IAppDbContext db,
        IClock clock,
        IPushSender push,
        IRealtimePublisher realtime)
    {
        _db = db;
        _clock = clock;
        _push = push;
        _realtime = realtime;
    }

    public async Task<EventBatchResponse> IngestAsync(
        Guid installationId,
        EventBatchRequest request,
        CancellationToken cancellationToken)
    {
        var installation = await _db.Installations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == installationId && i.IsActive, cancellationToken)
            ?? throw AppException.NotFound("Installation was not found.");

        if (request.Events.Count == 0)
            return new EventBatchResponse(0, 0);

        if (request.Events.Count > 100)
            throw AppException.BadRequest("A batch may contain at most 100 events.", "batch_too_large");

        var eventKeys = request.Events.Select(e => e.EventId).ToList();
        var existing = await _db.BusinessEvents
            .AsNoTracking()
            .Where(e => e.InstallationId == installationId && eventKeys.Contains(e.EventKey))
            .Select(e => e.EventKey)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var accepted = 0;
        var duplicates = 0;
        var newEvents = new List<BusinessEvent>();

        foreach (var item in request.Events)
        {
            if (string.IsNullOrWhiteSpace(item.EventId) || item.EventId.Length > 100 ||
                string.IsNullOrWhiteSpace(item.Type) || item.Type.Length > 100)
                throw AppException.BadRequest("Every event requires a valid id and type.", "invalid_event");

            ValidateJson(item.PayloadJson);

            if (!existingSet.Add(item.EventId))
            {
                duplicates++;
                continue;
            }

            var businessEvent = new BusinessEvent
            {
                TenantId = installation.TenantId,
                InstallationId = installation.Id,
                EventKey = item.EventId,
                Type = item.Type.Trim(),
                PayloadJson = string.IsNullOrWhiteSpace(item.PayloadJson) ? "{}" : item.PayloadJson,
                OccurredAt = item.OccurredAt == default ? _clock.UtcNow : item.OccurredAt
            };

            _db.BusinessEvents.Add(businessEvent);
            newEvents.Add(businessEvent);
            accepted++;
        }

        if (newEvents.Count == 0)
            return new EventBatchResponse(0, duplicates);

        var recipients = await _db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == installation.TenantId &&
                        m.IsActive &&
                        m.User.IsActive &&
                        (m.Role == TenantRole.Owner ||
                         m.Role == TenantRole.Admin ||
                         m.Role == TenantRole.Manager))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var notifications = new List<Notification>();
        foreach (var businessEvent in newEvents)
        {
            var content = BuildNotificationContent(businessEvent);
            if (content is null)
                continue;

            foreach (var userId in recipients)
            {
                notifications.Add(new Notification
                {
                    TenantId = installation.TenantId,
                    UserId = userId,
                    InstallationId = installation.Id,
                    Title = content.Value.Title,
                    Body = content.Value.Body,
                    Type = content.Value.Type
                });
            }
        }

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(cancellationToken);

        await DeliverNotificationsAsync(notifications, cancellationToken);
        return new EventBatchResponse(accepted, duplicates);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        Guid userId,
        Guid tenantId,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.TenantId == tenantId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return (await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await _db.Notifications.CountAsync(
            n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead,
            cancellationToken);

    public async Task<NotificationDto> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.UserId == userId,
            cancellationToken) ?? throw AppException.NotFound("Notification was not found.");

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(notification);
    }

    public async Task RegisterDeviceAsync(
        Guid userId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpoPushToken) ||
            !request.ExpoPushToken.StartsWith("ExponentPushToken[", StringComparison.Ordinal))
            throw AppException.BadRequest("A valid Expo push token is required.", "invalid_push_token");

        var token = await _db.DeviceTokens.FirstOrDefaultAsync(
            t => t.ExpoPushToken == request.ExpoPushToken,
            cancellationToken);

        if (token is null)
        {
            token = new DeviceToken
            {
                UserId = userId,
                ExpoPushToken = request.ExpoPushToken.Trim(),
                Platform = request.Platform.Trim()
            };
            _db.DeviceTokens.Add(token);
        }
        else
        {
            token.UserId = userId;
            token.Platform = request.Platform.Trim();
            token.IsActive = true;
            token.LastSeenAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task DeliverNotificationsAsync(
        IReadOnlyList<Notification> notifications,
        CancellationToken cancellationToken)
    {
        if (notifications.Count == 0)
            return;

        var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
        var tokens = await _db.DeviceTokens
            .AsNoTracking()
            .Where(t => t.IsActive && userIds.Contains(t.UserId))
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var dto = ToDto(notification);
            await _realtime.PublishNotificationAsync(notification.UserId, dto, cancellationToken);

            var userTokens = tokens
                .Where(t => t.UserId == notification.UserId)
                .Select(t => t.ExpoPushToken)
                .Distinct()
                .ToList();

            if (userTokens.Count == 0)
                continue;

            await _push.SendAsync(
                userTokens,
                notification.Title,
                notification.Body,
                new Dictionary<string, string>
                {
                    ["tenantId"] = notification.TenantId.ToString(),
                    ["installationId"] = notification.InstallationId?.ToString() ?? string.Empty,
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = notification.Type
                },
                cancellationToken);
        }
    }

    private static (string Title, string Body, string Type)? BuildNotificationContent(
        BusinessEvent businessEvent)
    {
        using var document = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(businessEvent.PayloadJson) ? "{}" : businessEvent.PayloadJson);
        var root = document.RootElement;

        string Read(string name) =>
            root.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;

        return businessEvent.Type switch
        {
            "order.created" => (
                "New order",
                $"{Read("code")} - {Read("total")}",
                "order"),
            "order.completed" => (
                "Order completed",
                Read("code"),
                "order"),
            "order.cancelled" => (
                "Order cancelled",
                Read("code"),
                "warning"),
            "payment.received" => (
                "Payment received",
                $"{Read("amount")} {Read("method")}".Trim(),
                "payment"),
            "user.created" => (
                "User added",
                Read("username"),
                "user"),
            "user.status_changed" => (
                "User status changed",
                $"{Read("username")} - {Read("isActive")}",
                "user"),
            "inventory.changed" => (
                "Inventory changed",
                $"{Read("productName")} - {Read("inventory")}",
                "inventory"),
            "system.alert" => (
                "System alert",
                Read("message"),
                "alert"),
            _ => null
        };
    }

    private static void ValidateJson(string? payload)
    {
        try
        {
            using var _ = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        }
        catch (JsonException)
        {
            throw AppException.BadRequest("Event payload must be valid JSON.", "invalid_payload");
        }
    }

    private static NotificationDto ToDto(Notification notification) =>
        new(
            notification.Id,
            notification.TenantId,
            notification.InstallationId,
            notification.Title,
            notification.Body,
            notification.Type,
            notification.IsRead,
            notification.CreatedAt);
}
