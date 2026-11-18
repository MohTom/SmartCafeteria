using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface IAccountService
{
    Task<IEnumerable<AccountEntry>> GetAllEntriesAsync();
    Task<IEnumerable<AccountEntry>> GetEntriesByTypeAsync(AccountEntryType type);
    Task<AccountEntry?> GetEntryByIdAsync(string id);
    Task<bool> AddEntryAsync(AccountEntry entry);
    Task<bool> UpdateEntryAsync(AccountEntry entry);
    Task<bool> DeleteEntryAsync(string id);
    Task<AccountingSummary> GetSummaryAsync(DateTime start, DateTime end);
    Task<IEnumerable<AccountEntry>> GetEntriesByDateRangeAsync(DateTime start, DateTime end);
}