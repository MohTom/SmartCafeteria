using Dapper;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class LogRepository : ILogService
{
    private readonly DbContext _dbContext;

    public LogRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<SystemLog>> GetAllLogsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<SystemLog>("SELECT * FROM SystemLogs ORDER BY Timestamp DESC");
    }

    public async Task<bool> AddLogAsync(string message, string level = "info")
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var log = new SystemLog
        {
            Id = $"log-{Guid.NewGuid():N}",
            Timestamp = DateTime.Now,
            Message = message,
            Level = level
        };
        await conn.ExecuteAsync(
            "INSERT INTO SystemLogs (Id, Timestamp, Message, Level) VALUES (@Id, @Timestamp, @Message, @Level)",
            log);
        return true;
    }

    public async Task<bool> ClearLogsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM SystemLogs");
        return true;
    }
}
