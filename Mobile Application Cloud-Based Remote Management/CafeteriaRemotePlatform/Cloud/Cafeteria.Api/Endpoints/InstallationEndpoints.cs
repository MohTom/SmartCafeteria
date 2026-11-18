using System.Security.Claims;
using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;

namespace Cafeteria.Api.Endpoints;

public static class InstallationEndpoints
{
    public static IEndpointRouteBuilder MapInstallationEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/api/tenants/{tenantId:guid}/installations")
            .WithTags("Installations")
            .RequireAuthorization();

        userGroup.MapGet("", async (
                Guid tenantId,
                ClaimsPrincipal user,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.GetForUserAsync(
                user.GetUserId(),
                tenantId,
                cancellationToken)));

        userGroup.MapGet("/{installationId:guid}", async (
                Guid tenantId,
                Guid installationId,
                ClaimsPrincipal user,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.GetForUserAsync(
                user.GetUserId(),
                tenantId,
                installationId,
                cancellationToken)));

        userGroup.MapPost("/claim-codes", async (
                Guid tenantId,
                ClaimsPrincipal user,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.CreateClaimCodeAsync(
                user.GetUserId(),
                tenantId,
                cancellationToken)));

        userGroup.MapPut("/{installationId:guid}/access", async (
                Guid tenantId,
                Guid installationId,
                ClaimsPrincipal user,
                UpdateInstallationAccessRequest request,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.UpdateAccessAsync(
                user.GetUserId(),
                tenantId,
                installationId,
                request,
                cancellationToken)));

        var anonymous = app.MapGroup("/api/installations").WithTags("Installations");

        anonymous.MapPost("/claim", async (
                ClaimInstallationRequest request,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.ClaimAsync(request, cancellationToken)))
            .RequireRateLimiting("auth");

        anonymous.MapPost("/token", async (
                InstallationTokenRequest request,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            Results.Ok(await installations.CreateTokenAsync(request, cancellationToken)))
            .RequireRateLimiting("auth");

        var installationGroup = app.MapGroup("/api/installations")
            .WithTags("Installations")
            .RequireAuthorization(JwtAuthenticationExtensions.InstallationScheme);

        installationGroup.MapPost("/heartbeat", async (
                ClaimsPrincipal user,
                HeartbeatRequest request,
                InstallationService installations,
                CancellationToken cancellationToken) =>
            {
                await installations.HeartbeatAsync(
                    user.GetInstallationId(),
                    request,
                    cancellationToken);
                return Results.NoContent();
            });

        installationGroup.MapGet("/commands/pending", async (
                ClaimsPrincipal user,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.GetPendingForInstallationAsync(
                user.GetInstallationId(),
                cancellationToken)));

        installationGroup.MapPost("/commands/{commandId:guid}/ack", async (
                Guid commandId,
                ClaimsPrincipal user,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.AcknowledgeAsync(
                user.GetInstallationId(),
                commandId,
                cancellationToken)));

        installationGroup.MapPost("/commands/{commandId:guid}/complete", async (
                Guid commandId,
                ClaimsPrincipal user,
                CompleteCommandRequest request,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.CompleteAsync(
                user.GetInstallationId(),
                commandId,
                request,
                cancellationToken)));

        installationGroup.MapPost("/events", async (
                ClaimsPrincipal user,
                EventBatchRequest request,
                EventService events,
                CancellationToken cancellationToken) =>
            Results.Ok(await events.IngestAsync(
                user.GetInstallationId(),
                request,
                cancellationToken)));

        return app;
    }
}
