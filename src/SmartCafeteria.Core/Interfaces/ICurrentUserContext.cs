using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;

/// <summary>The authenticated user for the current session. Used by the data layer
/// to enforce authorization at the service boundary (not only in the UI).</summary>
public interface ICurrentUserContext
{
    User? CurrentUser { get; }
    bool IsActiveAdmin { get; }
}
