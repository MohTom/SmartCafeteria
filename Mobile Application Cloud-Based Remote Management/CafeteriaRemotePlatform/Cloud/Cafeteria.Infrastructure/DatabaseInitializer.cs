using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cafeteria.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitializer");

        if (db.Database.GetMigrations().Any())
        {
            logger.LogInformation("Applying database migrations.");
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        logger.LogWarning(
            "No EF Core migrations were found. Creating the schema with EnsureCreated for development.");
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
