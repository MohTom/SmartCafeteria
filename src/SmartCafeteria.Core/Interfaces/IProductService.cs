using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Core.Interfaces;
public interface IProductService
{
    Task<IEnumerable<Product>> GetAllProductsAsync();
    Task<Product?> GetProductByIdAsync(string id);
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
    Task<IEnumerable<Product>> SearchProductsAsync(string query);
    Task<bool> AddProductAsync(Product product);
    Task<bool> UpdateProductAsync(Product product);
    Task<bool> DeleteProductAsync(string id);
    Task<bool> RestockProductAsync(string id);
    Task<IEnumerable<ProductCustomization>> GetCustomizationsAsync(string productId);
    Task<bool> AddCustomizationAsync(ProductCustomization customization);
    Task<bool> DeleteCustomizationAsync(string id);
    Task<bool> UpdateInventoryAsync(string productId, int quantity);
}
