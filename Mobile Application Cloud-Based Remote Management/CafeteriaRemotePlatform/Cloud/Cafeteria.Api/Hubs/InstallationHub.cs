using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Cafeteria.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cafeteria.Api.Hubs;

[Authorize(AuthenticationSchemes = JwtAuthenticationExtensions.InstallationScheme)]
public sealed class InstallationHub : Hub
{
    private readonly InstallationService _installations;
    private readonly CommandService _commands;

    public InstallationHub(InstallationService installations, CommandService commands)
    {
        _installations = installations;
        _commands = commands;
    }

    public override async Task OnConnectedAsync()
    {
        var installationId = Context.User!.GetInstallationId();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupNames.Installation(installationId));

        await _installations.HeartbeatAsync(
            installationId,
            new HeartbeatRequest(string.Empty),
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public Task Heartbeat(HeartbeatRequest request) =>
        _installations.HeartbeatAsync(
            Context.User!.GetInstallationId(),
            request,
            Context.ConnectionAborted);

    public async Task<IReadOnlyList<RemoteCommandDto>> GetPendingCommands()
    {
        var installationId = Context.User!.GetInstallationId();
        var commands = await _commands.GetPendingForInstallationAsync(
            installationId,
            Context.ConnectionAborted);

        foreach (var command in commands)
            await _commands.MarkDispatchedAsync(command.Id, Context.ConnectionAborted);

        return commands;
    }

    public Task<RemoteCommandDto> AcknowledgeCommand(Guid commandId) =>
        _commands.AcknowledgeAsync(
            Context.User!.GetInstallationId(),
            commandId,
            Context.ConnectionAborted);

    public Task<RemoteCommandDto> CompleteCommand(
        Guid commandId,
        CompleteCommandRequest request) =>
        _commands.CompleteAsync(
            Context.User!.GetInstallationId(),
            commandId,
            request,
            Context.ConnectionAborted);
}
