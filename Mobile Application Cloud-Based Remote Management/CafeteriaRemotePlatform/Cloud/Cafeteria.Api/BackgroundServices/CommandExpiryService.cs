using Cafeteria.Application;

namespace Cafeteria.Api.BackgroundServices;

public sealed class CommandExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandExpiryService> _logger;

    public CommandExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<CommandExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var commands = scope.ServiceProvider.GetRequiredService<CommandService>();
                    var expired = await commands.ExpireAsync(stoppingToken);
                    if (expired > 0)
                        _logger.LogInformation("Expired {Count} remote commands.", expired);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to expire remote commands.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
