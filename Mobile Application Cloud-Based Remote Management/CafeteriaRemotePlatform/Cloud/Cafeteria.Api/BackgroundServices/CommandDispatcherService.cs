using Cafeteria.Application;

namespace Cafeteria.Api.BackgroundServices;

public sealed class CommandDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandDispatcherService> _logger;

    public CommandDispatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<CommandDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await DispatchPendingAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch pending remote commands.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<CommandService>();
        var pusher = scope.ServiceProvider.GetRequiredService<IInstallationCommandPusher>();

        var pending = await commands.GetDispatchableAsync(cancellationToken);
        foreach (var command in pending)
        {
            await pusher.PushAsync(command.InstallationId, command, cancellationToken);
            await commands.MarkDispatchedAsync(command.Id, cancellationToken);
            _logger.LogInformation(
                "Dispatched command {CommandId} ({CommandType}) to installation {InstallationId}.",
                command.Id,
                command.Type,
                command.InstallationId);
        }
    }
}
