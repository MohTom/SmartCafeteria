using System.Security.Claims;
using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;

namespace Cafeteria.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("", async (
                Guid tenantId,
                bool? unreadOnly,
                ClaimsPrincipal user,
                EventService events,
                CancellationToken cancellationToken) =>
            Results.Ok(await events.GetNotificationsAsync(
                user.GetUserId(),
                tenantId,
                unreadOnly ?? false,
                cancellationToken)));

        group.MapGet("/unread-count", async (
                Guid tenantId,
                ClaimsPrincipal user,
                EventService events,
                CancellationToken cancellationToken) =>
            Results.Ok(new UnreadCountResponse(await events.GetUnreadCountAsync(
                user.GetUserId(),
                tenantId,
                cancellationToken))));

        group.MapPost("/{notificationId:guid}/read", async (
                Guid notificationId,
                ClaimsPrincipal user,
                EventService events,
                CancellationToken cancellationToken) =>
            Results.Ok(await events.MarkReadAsync(
                user.GetUserId(),
                notificationId,
                cancellationToken)));

        app.MapPost("/api/notifications/devices", async (
                ClaimsPrincipal user,
                RegisterDeviceRequest request,
                EventService events,
                CancellationToken cancellationToken) =>
            {
                await events.RegisterDeviceAsync(
                    user.GetUserId(),
                    request,
                    cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Notifications")
            .RequireAuthorization();

        return app;
    }
}
