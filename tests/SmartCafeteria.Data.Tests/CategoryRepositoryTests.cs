using SmartCafeteria.Core.Models;
using SmartCafeteria.Data.Database;
using SmartCafeteria.Data.Repositories;
using Xunit;

namespace SmartCafeteria.Data.Tests;

public class CategoryRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbContext _dbContext;

    public CategoryRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"smart_cafe_cat_{Guid.NewGuid():N}.db");
        _dbContext = new DbContext(_dbPath);
        new DatabaseInitializer(_dbContext).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task GetAll_SeedsBuiltIns()
    {
        var repo = new CategoryRepository(_dbContext);
        var all = (await repo.GetAllAsync()).ToList();
        Assert.Equal(9, all.Count);
        Assert.Contains(all, c => c.Id == "HotDrinks" && c.NameAr == "مشروبات ساخنة");
        Assert.Contains(all, c => c.Id == "Juices" && c.NameAr == "عصائر");
        Assert.Contains(all, c => c.Id == "Grills" && c.NameAr == "مشويات");
    }

    [Fact]
    public async Task AddUpdateDelete_RoundTrip()
    {
        var repo = new CategoryRepository(_dbContext);
        var added = await repo.AddAsync(new Category { NameAr = "عصائر", NameEn = "Juices" });
        Assert.False(string.IsNullOrWhiteSpace(added.Id));

        added.NameAr = "عصائر طازجة";
        Assert.True(await repo.UpdateAsync(added));
        var fetched = await repo.GetByIdAsync(added.Id);
        Assert.NotNull(fetched);
        Assert.Equal("عصائر طازجة", fetched.NameAr);

        Assert.True(await repo.DeleteAsync(added.Id));
        Assert.Null(await repo.GetByIdAsync(added.Id));
    }

    [Fact]
    public async Task Delete_BlockedWhenInUse()
    {
        var repo = new CategoryRepository(_dbContext);
        var productRepo = new ProductRepository(_dbContext);
        await productRepo.AddProductAsync(new Product
        {
            Id = "p-cat", NameAr = "قهوة", NameEn = "Coffee", Price = 10, Category = "HotDrinks"
        });

        Assert.True(await repo.IsInUseAsync("HotDrinks"));
        Assert.False(await repo.DeleteAsync("HotDrinks"));
        Assert.NotNull(await repo.GetByIdAsync("HotDrinks"));
    }
}
