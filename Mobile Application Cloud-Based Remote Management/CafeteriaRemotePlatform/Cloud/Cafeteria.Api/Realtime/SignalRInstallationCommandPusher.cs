using Cafeteria.Api.Hubs;
using Cafeteria.Application;
using Cafeteria.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Cafeteria.Api.Realtime;

public sealed class SignalRInstallationCommandPusher : IInstallationCommandPusher
{
    private readonly IHubContext<InstallationHub> _installations;

    public SignalRInstallationCommandPusher(IHubContext<InstallationHub> installations)
    {
        _installations = installations;
    }

    public Task PushAsync(
        Guid installationId,
        RemoteCommandDto command,
        CancellationToken cancellationToken) =>
        _installations.Clients.Group(GroupNames.Installation(installationId))
            .SendAsync("CommandReceived", command, cancellationToken);
}
