using System.Security.Claims;
using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;

namespace Cafeteria.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("", async (
                ClaimsPrincipal user,
                TenantService tenants,
                CancellationToken cancellationToken) =>
            Results.Ok(await tenants.GetTenantsAsync(user.GetUserId(), cancellationToken)));

        group.MapGet("/{tenantId:guid}/members", async (
                Guid tenantId,
                ClaimsPrincipal user,
                TenantService tenants,
                CancellationToken cancellationToken) =>
            Results.Ok(await tenants.GetMembersAsync(
                user.GetUserId(),
                tenantId,
                cancellationToken)));

        group.MapPost("/{tenantId:guid}/invitations", async (
                Guid tenantId,
                ClaimsPrincipal user,
                InviteMemberRequest request,
                TenantService tenants,
                CancellationToken cancellationToken) =>
            Results.Ok(await tenants.CreateInvitationAsync(
                user.GetUserId(),
                tenantId,
                request,
                cancellationToken)));

        return app;
    }
}
