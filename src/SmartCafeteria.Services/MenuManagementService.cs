using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Services;
public class MenuManagementService
{
    private readonly IProductService _productService;
    private readonly ILogService _logService;

    public MenuManagementService(IProductService productService, ILogService logService)
    {
        _productService = productService;
        _logService = logService;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        return await _productService.GetAllProductsAsync();
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        return await _productService.SearchProductsAsync(query);
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        return await _productService.GetProductsByCategoryAsync(category);
    }

    public async Task<bool> AddProductAsync(Product product)
    {
        var result = await _productService.AddProductAsync(product);
        if (result)
            await _logService.AddLogAsync($"تم إضافة منتج جديد: {product.NameAr}", "success");
        return result;
    }

    public async Task<bool> UpdateProductAsync(Product product)
    {
        var result = await _productService.UpdateProductAsync(product);
        if (result)
            await _logService.AddLogAsync($"تم تحديث المنتج: {product.NameAr}", "info");
        return result;
    }

    public async Task<bool> DeleteProductAsync(string productId)
    {
        var result = await _productService.DeleteProductAsync(productId);
        if (result)
            await _logService.AddLogAsync($"تم حذف المنتج", "warning");
        return result;
    }

    public async Task<bool> RestockProductAsync(string productId)
    {
        var result = await _productService.RestockProductAsync(productId);
        if (result)
            await _logService.AddLogAsync($"تم تعبئة المنتج", "success");
        return result;
    }

    public async Task<bool> CheckLowStockAsync(int threshold)
    {
        var products = await _productService.GetAllProductsAsync();
        var lowStockProducts = products.Where(p => p.Inventory <= threshold);
        foreach (var product in lowStockProducts)
        {
            if (product.Inventory == 0)
                await _logService.AddLogAsync($" تنبيه: المنتج {product.NameAr} نفد من المخزون", "alert");
            else
                await _logService.AddLogAsync($" تحذير: مخزون {product.NameAr} منخفض ({product.Inventory})", "warning");
        }
        return true;
    }
}
