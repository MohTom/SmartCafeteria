using Dapper;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class SettingsRepository : ISettingsService
{
    private readonly DbContext _dbContext;

    public SettingsRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var settings = await conn.QueryFirstOrDefaultAsync<AppSettings>("SELECT * FROM AppSettings WHERE Id = 1");
        return settings ?? new AppSettings();
    }

    public async Task<bool> SaveSettingsAsync(AppSettings settings)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO AppSettings 
            (Id, CurrencyAr, CurrencyEn, LowStockThreshold, CashierName, StationId, PrinterName, PrintMethod, CafeteriaNameAr, CafeteriaNameEn, CafeteriaAddress, CafeteriaPhone)
            VALUES 
            (1, @CurrencyAr, @CurrencyEn, @LowStockThreshold, @CashierName, @StationId, @PrinterName, @PrintMethod, @CafeteriaNameAr, @CafeteriaNameEn, @CafeteriaAddress, @CafeteriaPhone)",
            settings);
        return true;
    }
}
