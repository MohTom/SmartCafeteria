using System.Security.Claims;
using Cafeteria.Application;

namespace Cafeteria.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ??
                    principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw AppException.Unauthorized("The access token is missing a valid user id.");
    }

    public static Guid GetInstallationId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ??
                    principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw AppException.Unauthorized("The access token is missing a valid installation id.");
    }
}
