using Cafeteria.Api.Hubs;
using Cafeteria.Application;
using Cafeteria.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Cafeteria.Api.Realtime;

public sealed class SignalRRealtimePublisher : IRealtimePublisher
{
    private readonly IHubContext<UserHub> _users;
    private readonly IHubContext<InstallationHub> _installations;

    public SignalRRealtimePublisher(
        IHubContext<UserHub> users,
        IHubContext<InstallationHub> installations)
    {
        _users = users;
        _installations = installations;
    }

    public Task PublishCommandCreatedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken) =>
        _users.Clients.Group(GroupNames.User(requestedByUserId))
            .SendAsync("CommandCreated", command, cancellationToken);

    public Task PublishCommandCompletedAsync(
        Guid tenantId,
        Guid requestedByUserId,
        RemoteCommandDto command,
        CancellationToken cancellationToken) =>
        _users.Clients.Group(GroupNames.User(requestedByUserId))
            .SendAsync("CommandCompleted", command, cancellationToken);

    public Task PublishNotificationAsync(
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken) =>
        _users.Clients.Group(GroupNames.User(userId))
            .SendAsync("NotificationReceived", notification, cancellationToken);

    public Task PublishInstallationStatusAsync(
        Guid tenantId,
        InstallationDto installation,
        CancellationToken cancellationToken) =>
        _users.Clients.Group(GroupNames.Tenant(tenantId))
            .SendAsync("InstallationStatusChanged", installation, cancellationToken);
}
