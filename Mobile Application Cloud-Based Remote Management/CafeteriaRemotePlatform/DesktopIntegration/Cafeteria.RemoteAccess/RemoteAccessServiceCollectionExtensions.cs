using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartCafeteria.Core.Interfaces;

namespace Cafeteria.RemoteAccess;

public static class RemoteAccessServiceCollectionExtensions
{
    public static IServiceCollection AddCafeteriaRemoteAccess(
        this IServiceCollection services,
        Func<IServiceProvider, ICurrentUserContext> currentUserFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(currentUserFactory);

        services.AddSingleton(RemoteAccessPaths.CreateDefault());
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IRemoteAccessSettingsStore, RemoteAccessSettingsStore>();
        services.AddSingleton<CloudApiClient>();

        services.AddSingleton<RemoteAwareCurrentUserContext>(provider =>
            new RemoteAwareCurrentUserContext(currentUserFactory(provider)));
        services.AddSingleton<ICurrentUserContext>(provider =>
            provider.GetRequiredService<RemoteAwareCurrentUserContext>());
        services.AddSingleton<IRemoteExecutionContext>(provider =>
            provider.GetRequiredService<RemoteAwareCurrentUserContext>());

        services.AddSingleton<IEventOutbox>(provider =>
        {
            var paths = provider.GetRequiredService<RemoteAccessPaths>();
            return new FileEventOutbox(paths.OutboxFile);
        });

        services.AddSingleton(provider =>
        {
            var paths = provider.GetRequiredService<RemoteAccessPaths>();
            return new CommandLedger(paths.CommandLedgerFile);
        });

        services.AddSingleton<IDesktopCommandHandler, DesktopCommandHandler>();
        services.AddSingleton(provider =>
        {
            var paths = provider.GetRequiredService<RemoteAccessPaths>();
            return new DesktopEventCollector(
                provider.GetRequiredService<IOrderService>(),
                provider.GetRequiredService<IUserService>(),
                provider.GetRequiredService<IProductService>(),
                provider.GetRequiredService<ISettingsService>(),
                provider.GetRequiredService<IEventOutbox>(),
                paths.EventStateFile,
                provider.GetService<ILogger<DesktopEventCollector>>()
                    ?? NullLogger<DesktopEventCollector>.Instance);
        });

        services.AddSingleton<IRemoteAccessCoordinator, RemoteAccessCoordinator>();
        return services;
    }
}
