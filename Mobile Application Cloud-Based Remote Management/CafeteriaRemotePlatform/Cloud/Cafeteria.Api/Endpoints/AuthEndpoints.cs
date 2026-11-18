using System.Security.Claims;
using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;

namespace Cafeteria.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
                RegisterCompanyRequest request,
                AuthService auth,
                CancellationToken cancellationToken) =>
            Results.Ok(await auth.RegisterCompanyAsync(request, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        group.MapPost("/login", async (
                LoginRequest request,
                AuthService auth,
                CancellationToken cancellationToken) =>
            Results.Ok(await auth.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
                RefreshRequest request,
                AuthService auth,
                CancellationToken cancellationToken) =>
            Results.Ok(await auth.RefreshAsync(request, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        group.MapPost("/logout", async (
                LogoutRequest request,
                AuthService auth,
                CancellationToken cancellationToken) =>
            {
                await auth.LogoutAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        group.MapGet("/me", async (
                ClaimsPrincipal user,
                AuthService auth,
                CancellationToken cancellationToken) =>
            Results.Ok(await auth.GetProfileAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization();

        group.MapPost("/accept-invitation", async (
                ClaimsPrincipal user,
                AcceptInvitationRequest request,
                TenantService tenants,
                CancellationToken cancellationToken) =>
            Results.Ok(await tenants.AcceptInvitationAsync(
                user.GetUserId(),
                request,
                cancellationToken)))
            .RequireAuthorization();

        return app;
    }
}
