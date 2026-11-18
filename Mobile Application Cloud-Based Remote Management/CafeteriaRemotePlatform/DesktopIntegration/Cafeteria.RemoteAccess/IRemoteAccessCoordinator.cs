namespace Cafeteria.RemoteAccess;

public interface IRemoteAccessCoordinator
{
    RemoteAccessStatus Status { get; }
    event EventHandler<RemoteAccessStatus>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<RemoteLinkResult> LinkAsync(
        RemoteLinkRequest request,
        CancellationToken cancellationToken = default);
    Task UnlinkAsync(CancellationToken cancellationToken = default);
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
