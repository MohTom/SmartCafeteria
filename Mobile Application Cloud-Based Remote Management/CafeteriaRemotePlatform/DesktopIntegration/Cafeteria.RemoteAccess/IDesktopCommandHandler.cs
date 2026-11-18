using Cafeteria.Contracts;

namespace Cafeteria.RemoteAccess;

public interface IDesktopCommandHandler
{
    Task<CommandExecutionResponse> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default);
}
