using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class ProductSearchTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ProductRepository _repo;

    public ProductSearchTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_search_{Guid.NewGuid():N}.db");
        var dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(dbContext).InitializeAsync().GetAwaiter().GetResult();
        _repo = new ProductRepository(dbContext);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch (Exception) { }
    }

    private static Product Product(string id, string nameAr) => new()
    {
        Id = id,
        NameAr = nameAr,
        NameEn = "Item",
        Category = "HotDrinks",
        Price = 10,
        Inventory = 10,
        MaxInventory = 100
    };

    [Fact]
    public async Task Search_WithPercentSign_TreatsItAsALiteralCharacter()
    {
        await _repo.AddProductAsync(Product("p-pct", "خصم 50%"));
        await _repo.AddProductAsync(Product("p-plain", "قهوة"));

        var wildcardResults = (await _repo.SearchProductsAsync("%")).ToList();
        var literalResults = await _repo.SearchProductsAsync("50%");

        var onlyLiteral = Assert.Single(wildcardResults);
        Assert.Equal("p-pct", onlyLiteral.Id);
        var match = Assert.Single(literalResults);
        Assert.Equal("p-pct", match.Id);
    }

    [Fact]
    public async Task Search_WithUnderscore_TreatsItAsALiteralCharacter()
    {
        await _repo.AddProductAsync(Product("p-underscore", "صنف_خاص"));
        await _repo.AddProductAsync(Product("p-normal", "شاي"));

        var wildcardResults = (await _repo.SearchProductsAsync("_")).ToList();
        var literalResults = await _repo.SearchProductsAsync("صنف_");

        var onlyLiteral = Assert.Single(wildcardResults);
        Assert.Equal("p-underscore", onlyLiteral.Id);
        var match = Assert.Single(literalResults);
        Assert.Equal("p-underscore", match.Id);
    }

    [Fact]
    public async Task Search_ReturnsCustomizationsForEveryMatch()
    {
        var product = Product("p-custom", "عصير مانجو");
        product.CustomizationOptions.Add(new CustomizationOption
        {
            Id = "c-1",
            NameAr = "سكر إضافي",
            NameEn = "Extra sugar",
            Price = 2
        });
        await _repo.AddProductAsync(product);

        var results = (await _repo.SearchProductsAsync("مانجو")).ToList();

        var match = Assert.Single(results);
        var option = Assert.Single(match.CustomizationOptions);
        Assert.Equal("سكر إضافي", option.NameAr);
    }
}
