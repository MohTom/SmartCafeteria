using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface ILogService
{
    Task<IEnumerable<SystemLog>> GetAllLogsAsync();
    Task<bool> AddLogAsync(string message, string level = "info");
    Task<bool> ClearLogsAsync();
}
