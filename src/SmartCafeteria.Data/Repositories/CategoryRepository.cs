using Dapper;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class CategoryRepository : ICategoryService
{
    private readonly DbContext _dbContext;

    public CategoryRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM Categories ORDER BY SortOrder ASC, NameAr COLLATE NOCASE ASC");
    }

    public async Task<Category?> GetByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Category>(
            "SELECT * FROM Categories WHERE Id = @Id", new { Id = id });
    }

    public async Task<Category> AddAsync(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Id))
            category.Id = await GenerateUniqueIdAsync(category.NameEn);
        using var conn = await _dbContext.GetConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Categories WHERE Id = @Id", new { Id = category.Id });
        if (exists > 0)
            throw new InvalidOperationException($"Category '{category.Id}' already exists.");
        if (category.SortOrder == 0)
        {
            var max = await conn.ExecuteScalarAsync<int?>("SELECT MAX(SortOrder) FROM Categories");
            category.SortOrder = (max ?? -1) + 1;
        }
        await conn.ExecuteAsync(
            "INSERT INTO Categories (Id, NameAr, NameEn, SortOrder) VALUES (@Id, @NameAr, @NameEn, @SortOrder)",
            category);
        return category;
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Categories SET NameAr = @NameAr, NameEn = @NameEn, SortOrder = @SortOrder WHERE Id = @Id",
            category);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (await IsInUseAsync(id))
            return false;
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync("DELETE FROM Categories WHERE Id = @Id", new { Id = id });
        if (affected > 0)
        {
            // Tombstone the id so DatabaseInitializer won't re-seed a
            // deleted built-in category on the next startup.
            await conn.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS DeletedCategoryIds (Id TEXT PRIMARY KEY)");
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO DeletedCategoryIds (Id) VALUES (@Id)", new { Id = id });
        }
        return affected > 0;
    }

    public async Task<bool> IsInUseAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products WHERE Category = @Id", new { Id = id });
        return count > 0;
    }

    public async Task<int> GetProductCountAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products WHERE Category = @Id", new { Id = id });
    }

    public async Task<Dictionary<string, int>> GetProductCountsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var rows = await conn.QueryAsync<(string Category, int Count)>(
            "SELECT Category, COUNT(*) AS Count FROM Products GROUP BY Category");
        return rows.ToDictionary(r => r.Category, r => r.Count);
    }

    private async Task<string> GenerateUniqueIdAsync(string? nameEn)
    {
        var slug = new string((nameEn ?? string.Empty).Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        if (string.IsNullOrEmpty(slug))
            slug = $"cat-{Guid.NewGuid():N}"[..12];
        using var conn = await _dbContext.GetConnectionAsync();
        var candidate = slug;
        var suffix = 2;
        while (await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Categories WHERE Id = @Id", new { Id = candidate }) > 0)
        {
            candidate = $"{slug}-{suffix++}";
        }
        return candidate;
    }
}
