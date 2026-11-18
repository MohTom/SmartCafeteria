using Dapper;
using Microsoft.Data.Sqlite;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Data.Database;

namespace SmartCafeteria.Data.Repositories;
public class ProductRepository : IProductService
{
    private readonly DbContext _dbContext;

    public ProductRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync()
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var products = (await conn.QueryAsync<Product>("SELECT * FROM Products")).ToList();
        await LoadCustomizationsAsync(conn, products);
        return products;
    }

    /// <summary>Loads all customization rows for the given products in a single
    /// query (avoids one extra round-trip per product).</summary>
    private static async Task LoadCustomizationsAsync(SqliteConnection conn, List<Product> products)
    {
        if (products.Count == 0) return;

        var options = (await conn.QueryAsync<CustomizationOption>(
            "SELECT * FROM ProductCustomizations WHERE ProductId IN @Ids",
            new { Ids = products.Select(p => p.Id).ToList() })).ToList();
        var byProduct = options.GroupBy(o => o.ProductId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var product in products)
        {
            product.CustomizationOptions = byProduct.TryGetValue(product.Id, out var list)
                ? list
                : new List<CustomizationOption>();
        }
    }

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var product = await conn.QueryFirstOrDefaultAsync<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = id });
        if (product != null)
        {
            product.CustomizationOptions = (await conn.QueryAsync<CustomizationOption>(
                "SELECT * FROM ProductCustomizations WHERE ProductId = @ProductId",
                new { ProductId = id })).ToList();
        }
        return product;
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        if (category == "all") return await GetAllProductsAsync();

        var products = (await conn.QueryAsync<Product>(
            "SELECT * FROM Products WHERE Category = @Category",
            new { Category = category })).ToList();
        await LoadCustomizationsAsync(conn, products);
        return products;
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var escaped = (query ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var searchPattern = $"%{escaped}%";
        var products = (await conn.QueryAsync<Product>(
            "SELECT * FROM Products WHERE NameAr LIKE @Pattern ESCAPE '\\' OR NameEn LIKE @Pattern ESCAPE '\\' OR DescriptionAr LIKE @Pattern ESCAPE '\\' OR DescriptionEn LIKE @Pattern ESCAPE '\\'",
            new { Pattern = searchPattern })).ToList();
        await LoadCustomizationsAsync(conn, products);
        return products;
    }

    public async Task<bool> AddProductAsync(Product product)
    {
        SmartCafeteria.Data.Validation.BusinessRules.ValidateProduct(product);
        if (string.IsNullOrEmpty(product.Id))
            product.Id = $"p-{Guid.NewGuid():N}";
        foreach (var opt in product.CustomizationOptions)
        {
            if (string.IsNullOrEmpty(opt.Id))
                opt.Id = $"c-{Guid.NewGuid():N}";
            opt.ProductId = product.Id;
        }
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(
                "INSERT INTO Products (Id, NameAr, NameEn, DescriptionAr, DescriptionEn, Price, Category, ImageUrl, Emoji, Inventory, MaxInventory, Barcode) VALUES (@Id, @NameAr, @NameEn, @DescriptionAr, @DescriptionEn, @Price, @Category, @ImageUrl, @Emoji, @Inventory, @MaxInventory, @Barcode)",
                product, transaction);
            foreach (var opt in product.CustomizationOptions)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO ProductCustomizations (Id, ProductId, NameAr, NameEn, Price) VALUES (@Id, @ProductId, @NameAr, @NameEn, @Price)",
                    opt, transaction);
            }
            transaction.Commit();
            return true;
        }
        catch
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Transaction rollback failed: {0}", rollbackError);
            }
            throw;
        }
    }

    public async Task<bool> UpdateProductAsync(Product product)
    {
        SmartCafeteria.Data.Validation.BusinessRules.ValidateProduct(product);
        using var conn = await _dbContext.GetConnectionAsync();
        using var transaction = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(
                "UPDATE Products SET NameAr=@NameAr, NameEn=@NameEn, DescriptionAr=@DescriptionAr, DescriptionEn=@DescriptionEn, Price=@Price, Category=@Category, ImageUrl=@ImageUrl, Emoji=@Emoji, Inventory=@Inventory, MaxInventory=@MaxInventory, Barcode=@Barcode WHERE Id=@Id",
                product, transaction);
            await conn.ExecuteAsync("DELETE FROM ProductCustomizations WHERE ProductId = @ProductId", new { ProductId = product.Id }, transaction);
            foreach (var opt in product.CustomizationOptions)
            {
                if (string.IsNullOrEmpty(opt.Id))
                    opt.Id = $"c-{Guid.NewGuid():N}";
                opt.ProductId = product.Id;
                await conn.ExecuteAsync(
                    "INSERT INTO ProductCustomizations (Id, ProductId, NameAr, NameEn, Price) VALUES (@Id, @ProductId, @NameAr, @NameEn, @Price)",
                    opt, transaction);
            }
            transaction.Commit();
            return true;
        }
        catch
        {
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Transaction rollback failed: {0}", rollbackError);
            }
            throw;
        }
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM ProductCustomizations WHERE ProductId = @ProductId", new { ProductId = id });
        var affected = await conn.ExecuteAsync("DELETE FROM Products WHERE Id = @Id", new { Id = id });
        return affected > 0;
    }

    public async Task<bool> RestockProductAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Products SET Inventory = MaxInventory WHERE Id = @Id",
            new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<ProductCustomization>> GetCustomizationsAsync(string productId)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        return await conn.QueryAsync<ProductCustomization>(
            "SELECT * FROM ProductCustomizations WHERE ProductId = @ProductId",
            new { ProductId = productId });
    }

    public async Task<bool> AddCustomizationAsync(ProductCustomization customization)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO ProductCustomizations (Id, ProductId, NameAr, NameEn, Price) VALUES (@Id, @ProductId, @NameAr, @NameEn, @Price)",
            customization);
        return true;
    }

    public async Task<bool> DeleteCustomizationAsync(string id)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync("DELETE FROM ProductCustomizations WHERE Id = @Id", new { Id = id });
        return affected > 0;
    }

    public async Task<bool> UpdateInventoryAsync(string productId, int quantity)
    {
        using var conn = await _dbContext.GetConnectionAsync();
        var affected = await conn.ExecuteAsync(
            "UPDATE Products SET Inventory = Inventory - @Quantity WHERE Id = @Id AND Inventory >= @Quantity",
            new { Id = productId, Quantity = quantity });
        return affected > 0;
    }
}
