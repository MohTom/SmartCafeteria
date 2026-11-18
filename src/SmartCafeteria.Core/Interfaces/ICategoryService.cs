using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(string id);
    Task<Category> AddAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    /// <summary>Deletes a category. Returns false when products still use it.</summary>
    Task<bool> DeleteAsync(string id);
    Task<bool> IsInUseAsync(string id);
    Task<int> GetProductCountAsync(string id);
    Task<Dictionary<string, int>> GetProductCountsAsync();
}
