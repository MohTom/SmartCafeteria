using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace Cafeteria.RemoteAccess;

public interface IRemoteExecutionContext
{
    IDisposable BeginRemoteAdmin(Guid remoteUserId, string displayName);
}

public sealed class RemoteAwareCurrentUserContext : ICurrentUserContext, IRemoteExecutionContext
{
    private readonly ICurrentUserContext _inner;
    private readonly AsyncLocal<RemoteAdminScope?> _scope = new();

    public RemoteAwareCurrentUserContext(ICurrentUserContext inner)
    {
        _inner = inner;
    }

    public User? CurrentUser
    {
        get
        {
            var scope = _scope.Value;
            return scope is null ? _inner.CurrentUser : scope.User;
        }
    }

    public bool IsActiveAdmin => _scope.Value is not null || _inner.IsActiveAdmin;

    public IDisposable BeginRemoteAdmin(Guid remoteUserId, string displayName)
    {
        var previous = _scope.Value;
        _scope.Value = new RemoteAdminScope(new User
        {
            Id = $"remote-{remoteUserId:N}",
            Username = "remote-admin",
            FullName = string.IsNullOrWhiteSpace(displayName)
                ? "Remote administrator"
                : displayName,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.Now
        });

        return new ScopeRestorer(this, previous);
    }

    private sealed record RemoteAdminScope(User User);

    private sealed class ScopeRestorer : IDisposable
    {
        private readonly RemoteAwareCurrentUserContext _owner;
        private readonly RemoteAdminScope? _previous;

        public ScopeRestorer(
            RemoteAwareCurrentUserContext owner,
            RemoteAdminScope? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose() => _owner._scope.Value = _previous;
    }
}
