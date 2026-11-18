using System.Security.Claims;
using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;

namespace Cafeteria.Api.Endpoints;

public static class CommandEndpoints
{
    public static IEndpointRouteBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/commands")
            .WithTags("Commands")
            .RequireAuthorization();

        group.MapPost("", async (
                Guid tenantId,
                ClaimsPrincipal user,
                CreateCommandRequest request,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.CreateAsync(
                user.GetUserId(),
                tenantId,
                request,
                cancellationToken)));

        group.MapGet("", async (
                Guid tenantId,
                Guid? installationId,
                ClaimsPrincipal user,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.ListAsync(
                user.GetUserId(),
                tenantId,
                installationId,
                cancellationToken)));

        group.MapGet("/{commandId:guid}", async (
                Guid tenantId,
                Guid commandId,
                ClaimsPrincipal user,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.GetAsync(
                user.GetUserId(),
                tenantId,
                commandId,
                cancellationToken)));

        group.MapPost("/{commandId:guid}/cancel", async (
                Guid tenantId,
                Guid commandId,
                ClaimsPrincipal user,
                CommandService commands,
                CancellationToken cancellationToken) =>
            Results.Ok(await commands.CancelAsync(
                user.GetUserId(),
                tenantId,
                commandId,
                cancellationToken)));

        return app;
    }
}
