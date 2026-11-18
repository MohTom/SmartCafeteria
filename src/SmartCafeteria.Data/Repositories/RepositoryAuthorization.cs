using SmartCafeteria.Core.Interfaces;

namespace SmartCafeteria.Data.Repositories;

/// <summary>Enforces administrator-only data changes at the repository boundary,
/// so hiding a screen in the UI is never the only protection.</summary>
internal static class RepositoryAuthorization
{
    public static void EnsureAdmin(ICurrentUserContext currentUser, string operation)
    {
        if (!currentUser.IsActiveAdmin)
            throw new UnauthorizedAccessException($"{operation} requires an active administrator.");
    }
}
