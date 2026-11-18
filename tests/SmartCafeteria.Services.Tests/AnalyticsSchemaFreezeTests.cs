using Dapper;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using SmartCafeteria.Services;
using SmartCafeteria.TestSupport;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Guards the promise that the analytical reports are read-only: the deployed
/// database must keep its exact shape and its exact contents after every report
/// has been built, because the app is already installed in the field.
/// </summary>
public class AnalyticsSchemaFreezeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;

    public AnalyticsSchemaFreezeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_analytics_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(_dbContext).InitializeAsync().GetAwaiter().GetResult();
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

    private AnalyticsService CreateService()
    {
        var products = new ProductRepository(_dbContext);
        var orders = new OrderRepository(_dbContext, products);
        var accounts = new AccountRepository(_dbContext, orders, TestCurrentUser.Admin());

        return new AnalyticsService(
            orders,
            products,
            accounts,
            new SupplierRepository(_dbContext, TestCurrentUser.Admin()),
            new EmployeeRepository(_dbContext, TestCurrentUser.Admin()),
            new LogRepository(_dbContext),
            new SettingsRepository(_dbContext));
    }

    private async Task<Dictionary<string, List<string>>> GetSchemaAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var tables = (await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")).ToList();

        var schema = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var columns = (await conn.QueryAsync<string>(
                $"SELECT name FROM pragma_table_info('{table}') ORDER BY name")).ToList();
            schema[table] = columns;
        }

        return schema;
    }

    private async Task<Dictionary<string, long>> GetRowCountsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var tables = (await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")).ToList();

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
            counts[table] = await conn.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}");

        return counts;
    }

    [Fact]
    public async Task TheDatabaseStillHasExactlyTheSixteenFrozenTables()
    {
        var schema = await GetSchemaAsync();

        // 15 original tables + OrderCounters (atomic receipt numbering, Phase 7).
        Assert.Equal(16, schema.Count);
        Assert.Contains("Products", schema.Keys);
        Assert.Contains("AccountEntries", schema.Keys);
        Assert.Contains("Employees", schema.Keys);
        Assert.Contains("OrderCounters", schema.Keys);
        Assert.DoesNotContain(schema.Keys, t => t.Contains("Analytics", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildingEveryAnalyticsReport_DoesNotChangeTheSchema()
    {
        var schemaBefore = await GetSchemaAsync();

        var bundle = await CreateService().BuildAsync(null, null);

        Assert.Equal(18, bundle.Reports.Count);
        Assert.Equal(schemaBefore, await GetSchemaAsync());
    }

    [Fact]
    public async Task BuildingEveryAnalyticsReport_DoesNotWriteASingleRow()
    {
        var countsBefore = await GetRowCountsAsync();

        await CreateService().BuildAsync(DateTime.Today.AddMonths(-1), DateTime.Today);

        Assert.Equal(countsBefore, await GetRowCountsAsync());
    }

    [Fact]
    public async Task BuildingTheReportsTwice_StillDoesNotWrite()
    {
        var service = CreateService();
        var countsBefore = await GetRowCountsAsync();

        await service.BuildAsync(null, null);
        await service.BuildAsync(null, null);

        Assert.Equal(countsBefore, await GetRowCountsAsync());
    }
}

