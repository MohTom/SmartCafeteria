using Dapper;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

/// <summary>
/// Guards the deployed database. New user preferences must be stored in a config
/// file, never as a new column or table, so the new build keeps working against
/// the database of an already installed, working app.
/// </summary>
public class SchemaFreezeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;
    private readonly DatabaseInitializer _initializer;

    public SchemaFreezeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_schema_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        _initializer = new DatabaseInitializer(_dbContext);
        _initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private async Task<List<string>> GetColumnsAsync(string table)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var rows = await conn.QueryAsync<string>(
            $"SELECT name FROM pragma_table_info('{table}') ORDER BY name");
        return rows.ToList();
    }

    private async Task<List<string>> GetTablesAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var rows = await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        return rows.ToList();
    }

    [Fact]
    public async Task AppSettings_Table_HasNoFontSizeColumn()
    {
        var columns = await GetColumnsAsync("AppSettings");

        Assert.DoesNotContain(columns, c => c.Contains("Font", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("Ticket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AppSettings_Table_KeepsItsFrozenColumnSet()
    {
        var columns = await GetColumnsAsync("AppSettings");

        Assert.Equal(
            new[]
            {
                "CafeteriaAddress", "CafeteriaNameAr", "CafeteriaNameEn", "CafeteriaPhone",
                "CashierName", "CurrencyAr", "CurrencyEn", "Id", "LowStockThreshold",
                "PrintMethod", "PrinterName", "StationId"
            },
            columns);
    }

    [Fact]
    public void AppSettingsModel_HasNoFontSizeProperty()
    {
        var properties = typeof(AppSettings)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(properties, p => p.Contains("Font", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Contains("Ticket", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SavingSettings_DoesNotChangeTheSchema()
    {
        var columnsBefore = await GetColumnsAsync("AppSettings");
        var tablesBefore = await GetTablesAsync();

        var repo = new SettingsRepository(_dbContext);
        await repo.SaveSettingsAsync(new AppSettings
        {
            CurrencyAr = "ج.س",
            CurrencyEn = "SDG",
            LowStockThreshold = 5,
            CashierName = "الكاشير",
            StationId = "TERMINAL-01",
            PrinterName = "XP-80C",
            PrintMethod = Core.Enums.PrintMethod.FullReceiptAndSectionReceipts,
            CafeteriaNameAr = "كافتيريا التميز",
            CafeteriaNameEn = "AL-TAMAYUZ CAFETERIA",
            CafeteriaAddress = "شارع النيل الخرطوم",
            CafeteriaPhone = "+249 123 456 789"
        });

        var columnsAfter = await GetColumnsAsync("AppSettings");
        var tablesAfter = await GetTablesAsync();

        Assert.Equal(columnsBefore, columnsAfter);
        Assert.Equal(tablesBefore, tablesAfter);
    }

    [Fact]
    public async Task SavingSettings_KeepsExactlyOneSettingsRow()
    {
        var repo = new SettingsRepository(_dbContext);
        var settings = await repo.GetSettingsAsync();

        await repo.SaveSettingsAsync(settings);
        await repo.SaveSettingsAsync(settings);

        using var conn = await _dbContext.GetConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppSettings");

        Assert.Equal(1, count);
    }
}
