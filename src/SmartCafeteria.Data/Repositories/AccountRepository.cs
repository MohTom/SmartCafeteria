using Dapper;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class AccountRepository : IAccountService
{
    private readonly DbContext _dbContext;
    private readonly IOrderService _orderService;
    private readonly ICurrentUserContext _currentUser;

    public AccountRepository(DbContext dbContext, IOrderService orderService, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _orderService = orderService;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<AccountEntry>> GetAllEntriesAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<AccountEntry>("SELECT * FROM AccountEntries ORDER BY Date DESC");
    }

    public async Task<IEnumerable<AccountEntry>> GetEntriesByDateRangeAsync(DateTime start, DateTime end)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<AccountEntry>(
            "SELECT * FROM AccountEntries WHERE Date >= @Start AND Date <= @End ORDER BY Date DESC",
            new { Start = start, End = end });
    }

    public async Task<IEnumerable<AccountEntry>> GetEntriesByTypeAsync(AccountEntryType type)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<AccountEntry>(
            "SELECT * FROM AccountEntries WHERE Type = @Type ORDER BY Date DESC",
            new { Type = (int)type });
    }

    public async Task<AccountEntry?> GetEntryByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<AccountEntry>(
            "SELECT * FROM AccountEntries WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> AddEntryAsync(AccountEntry entry)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Adding an accounting entry");
        using var conn = await _dbContext.GetConnectionAsync();
        if (string.IsNullOrEmpty(entry.Id))
            entry.Id = $"acc-{Guid.NewGuid():N}";
        var affected = await conn.ExecuteAsync(
            "INSERT INTO AccountEntries (Id, Type, Category, Description, Amount, Date, PaymentMethod, ReferenceCode, UserName) VALUES (@Id, @Type, @Category, @Description, @Amount, @Date, @PaymentMethod, @ReferenceCode, @UserName)",
            entry);
        return affected > 0;
    }

    public async Task<bool> UpdateEntryAsync(AccountEntry entry)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Updating an accounting entry");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE AccountEntries SET Type=@Type, Category=@Category, Description=@Description, Amount=@Amount, Date=@Date, PaymentMethod=@PaymentMethod, ReferenceCode=@ReferenceCode, UserName=@UserName WHERE Id=@Id",
            entry);
        return affected > 0;
    }

    public async Task<bool> DeleteEntryAsync(string id)
    {
        RepositoryAuthorization.EnsureAdmin(_currentUser, "Deleting an accounting entry");
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync("DELETE FROM AccountEntries WHERE Id = @Id", new { Id = id });
        return affected > 0;
    }

    public async Task<AccountingSummary> GetSummaryAsync(DateTime start, DateTime end)
    {
        var orders = await _orderService.GetOrdersByDateRangeAsync(start, end);
        var validOrders = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var entries = await GetEntriesByDateRangeAsync(start, end);
        var entriesList = entries.ToList();

        return new AccountingSummary
        {
            IncomeFromOrders = validOrders.Sum(o => o.Total),
            OtherIncome = entriesList.Where(e => e.Type == AccountEntryType.Income).Sum(e => e.Amount),
            TotalExpenses = entriesList.Where(e => e.Type == AccountEntryType.Expense).Sum(e => e.Amount),
            OrderCount = validOrders.Count
        };
    }
}