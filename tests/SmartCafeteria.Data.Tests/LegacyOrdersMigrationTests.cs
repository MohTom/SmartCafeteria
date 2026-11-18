using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

/// <summary>
/// The app is already installed in the field, so its database does not have the
/// order-type and note columns yet. Startup must add them in place, keep every
/// existing order and read it back as محلي without a note.
/// </summary>
public class LegacyOrdersMigrationTests : IDisposable
{
    private const string LegacyOrdersDdl = """
        CREATE TABLE Orders (
            Id TEXT PRIMARY KEY,
            Code TEXT NOT NULL,
            Subtotal REAL NOT NULL,
            CustomizationsTotal REAL NOT NULL,
            Total REAL NOT NULL,
            PaymentMethod INTEGER NOT NULL,
            PaymentStatus TEXT DEFAULT 'paid',
            Status INTEGER NOT NULL,
            Timestamp TEXT NOT NULL,
            StationId TEXT DEFAULT '',
            CashierName TEXT DEFAULT '',
            IsPreOrder INTEGER DEFAULT 0,
            PickupTime TEXT
        );
        """;

    private readonly string _dbPath;

    public LegacyOrdersMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_legacy_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
        catch
        {
            // A leftover temp file must not fail the suite.
        }
    }

    private async Task CreateLegacyDatabaseAsync()
    {
        var dbContext = new DbContext(_dbPath);
        using var conn = await dbContext.GetConnectionAsync();
        await conn.ExecuteAsync(LegacyOrdersDdl);
        await conn.ExecuteAsync(
            "INSERT INTO Orders (Id, Code, Subtotal, CustomizationsTotal, Total, PaymentMethod, PaymentStatus, Status, Timestamp, StationId, CashierName, IsPreOrder, PickupTime) " +
            "VALUES ('legacy-1', '1', 30, 0, 30, 0, 'paid', 0, '2026-01-05 10:00:00', 'T1', 'أحمد', 0, NULL)");
    }

    private async Task<List<string>> GetOrderColumnsAsync()
    {
        var dbContext = new DbContext(_dbPath);
        using var conn = await dbContext.GetConnectionAsync();
        var rows = await conn.QueryAsync<string>("SELECT name FROM pragma_table_info('Orders') ORDER BY name");
        return rows.ToList();
    }

    [Fact]
    public async Task Startup_AddsTheOrderTypeAndNoteColumnsToTheLegacyOrdersTable()
    {
        await CreateLegacyDatabaseAsync();

        await new DatabaseInitializer(new DbContext(_dbPath)).InitializeAsync();

        var columns = await GetOrderColumnsAsync();
        Assert.Contains("OrderType", columns);
        Assert.Contains("Notes", columns);
    }

    [Fact]
    public async Task Startup_ReadsTheLegacyOrderAsLocalWithoutANote()
    {
        await CreateLegacyDatabaseAsync();

        await new DatabaseInitializer(new DbContext(_dbPath)).InitializeAsync();

        var dbContext = new DbContext(_dbPath);
        var repo = new OrderRepository(dbContext, new ProductRepository(dbContext));
        var order = await repo.GetOrderByIdAsync("legacy-1");

        Assert.NotNull(order);
        Assert.Equal(30m, order.Total);
        Assert.Equal(OrderType.Local, order.OrderType);
        Assert.Equal(string.Empty, order.Notes);
    }

    [Fact]
    public async Task Startup_IsIdempotentSoItCanRunOnEveryLaunch()
    {
        await CreateLegacyDatabaseAsync();

        var initializer = new DatabaseInitializer(new DbContext(_dbPath));
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        var columns = await GetOrderColumnsAsync();
        Assert.Single(columns, c => c == "OrderType");
        Assert.Single(columns, c => c == "Notes");
    }
}
