using Cafeteria.RemoteAccess;
using Moq;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Licensing.Service;
using SmartCafeteria.ViewModels;
using Xunit;

namespace SmartCafeteria.ViewModels.Tests;

public class InventoryTrackingViewModelTests
{
    [Fact]
    public void MenuViewModel_AllowsZeroStockAdd_WhenTrackingIsDisabled()
    {
        var viewModel = CreateMenuViewModel();
        viewModel.EnforceInventoryTracking = false;
        var product = Product("p-zero", inventory: 0);

        viewModel.QuickAddCommand.Execute(product);

        var item = Assert.Single(viewModel.Cart);
        Assert.Equal(product.Id, item.Product.Id);
    }

    [Fact]
    public void MenuViewModel_BlocksZeroStockAdd_WhenTrackingIsEnabled()
    {
        var viewModel = CreateMenuViewModel();
        viewModel.EnforceInventoryTracking = true;

        viewModel.QuickAddCommand.Execute(Product("p-zero", inventory: 0));

        Assert.Empty(viewModel.Cart);
    }

    [Fact]
    public void MenuViewModel_BlocksZeroStockCustomization_WhenTrackingIsEnabled()
    {
        var viewModel = CreateMenuViewModel();
        viewModel.EnforceInventoryTracking = true;

        viewModel.OpenCustomizationCommand.Execute(Product("p-zero", inventory: 0));

        Assert.False(viewModel.ShowCustomizationDialog);
    }

    [Fact]
    public void MenuViewModel_AllowsZeroStockCustomization_WhenTrackingIsDisabled()
    {
        var viewModel = CreateMenuViewModel();
        viewModel.EnforceInventoryTracking = false;
        var product = Product("p-zero", inventory: 0);

        viewModel.OpenCustomizationCommand.Execute(product);

        Assert.True(viewModel.ShowCustomizationDialog);
        Assert.Same(product, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task InventoryViewModel_AddProduct_UsesStockDefaults_WhenTrackingIsDisabled()
    {
        Product? added = null;
        var viewModel = CreateInventoryViewModel(product => added = product);
        viewModel.EnforceInventoryTracking = false;
        viewModel.NewNameAr = "منتج";
        viewModel.NewNameEn = "Product";
        viewModel.NewInventory = 7;
        viewModel.NewMaxInventory = 50;

        await viewModel.AddProductCommand.ExecuteAsync(null);

        Assert.NotNull(added);
        Assert.Equal(0, added.Inventory);
        Assert.Equal(100, added.MaxInventory);
    }

    [Fact]
    public async Task InventoryViewModel_AddProduct_KeepsStockValues_WhenTrackingIsEnabled()
    {
        Product? added = null;
        var viewModel = CreateInventoryViewModel(product => added = product);
        viewModel.EnforceInventoryTracking = true;
        viewModel.NewNameAr = "منتج";
        viewModel.NewNameEn = "Product";
        viewModel.NewInventory = 7;
        viewModel.NewMaxInventory = 50;

        await viewModel.AddProductCommand.ExecuteAsync(null);

        Assert.NotNull(added);
        Assert.Equal(7, added.Inventory);
        Assert.Equal(50, added.MaxInventory);
    }

    [Fact]
    public async Task InventoryViewModel_SaveEdit_PreservesHiddenStockValues_WhenTrackingIsDisabled()
    {
        Product? updated = null;
        var viewModel = CreateInventoryViewModel(updatedProduct: product => updated = product);
        var product = Product("p-edit", inventory: 5);
        product.MaxInventory = 20;
        viewModel.StartEditCommand.Execute(product);
        viewModel.EnforceInventoryTracking = false;
        viewModel.EditInventory = 999;
        viewModel.EditMaxInventory = 888;

        await viewModel.SaveEditCommand.ExecuteAsync(null);

        Assert.NotNull(updated);
        Assert.Equal(5, updated.Inventory);
        Assert.Equal(20, updated.MaxInventory);
    }

    [Fact]
    public async Task InventoryViewModel_SaveEdit_PreservesStoredImageUrl()
    {
        Product? updated = null;
        var viewModel = CreateInventoryViewModel(updatedProduct: product => updated = product);
        var product = Product("p-edit", inventory: 5);
        product.ImageUrl = "https://example.test/product.png";
        viewModel.StartEditCommand.Execute(product);
        viewModel.EditNameAr = "اسم جديد";

        await viewModel.SaveEditCommand.ExecuteAsync(null);

        Assert.NotNull(updated);
        Assert.Equal("https://example.test/product.png", updated.ImageUrl);
    }

    [Fact]
    public async Task SettingsViewModel_SavesInventoryTrackingPreference()
    {
        var store = new FakeTicketConfigStore { EnforceInventoryTracking = false };
        var settingsService = new Mock<ISettingsService>();
        settingsService
            .Setup(s => s.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .ReturnsAsync(true);
        var logService = new Mock<ILogService>();
        logService
            .Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        var categoryService = new Mock<ICategoryService>();
        categoryService
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(Array.Empty<Category>());
        categoryService
            .Setup(s => s.GetProductCountsAsync())
            .ReturnsAsync(new Dictionary<string, int>());

        var remoteAccess = new Mock<IRemoteAccessCoordinator>();
        remoteAccess.SetupGet(s => s.Status).Returns(RemoteAccessStatus.Unlinked);

        var viewModel = new SettingsViewModel(
            settingsService.Object,
            logService.Object,
            categoryService.Object,
            Mock.Of<IPrintService>(),
            store,
            new FakeCurrentUser(),
            new ActivationViewModel(Mock.Of<ILicenseService>()),
            remoteAccess.Object);

        Assert.False(viewModel.EnforceInventoryTracking);
        viewModel.EnforceInventoryTracking = true;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(store.EnforceInventoryTracking);
        settingsService.Verify(s => s.SaveSettingsAsync(It.IsAny<AppSettings>()), Times.Once);
    }

    private static MenuViewModel CreateMenuViewModel() => new(
        Mock.Of<IProductService>(),
        Mock.Of<ILogService>(),
        Mock.Of<ICategoryService>(),
        new FakeTicketConfigStore());

    private static InventoryViewModel CreateInventoryViewModel(
        Action<Product>? addedProduct = null,
        Action<Product>? updatedProduct = null)
    {
        var productService = new Mock<IProductService>();
        productService
            .Setup(s => s.GetAllProductsAsync())
            .ReturnsAsync(Array.Empty<Product>());
        productService
            .Setup(s => s.AddProductAsync(It.IsAny<Product>()))
            .Callback<Product>(product => addedProduct?.Invoke(product))
            .ReturnsAsync(true);
        productService
            .Setup(s => s.UpdateProductAsync(It.IsAny<Product>()))
            .Callback<Product>(product => updatedProduct?.Invoke(product))
            .ReturnsAsync(true);

        var logService = new Mock<ILogService>();
        logService
            .Setup(s => s.AddLogAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var categoryService = new Mock<ICategoryService>();
        categoryService
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(Array.Empty<Category>());

        return new InventoryViewModel(
            productService.Object,
            logService.Object,
            categoryService.Object,
            new FakeTicketConfigStore());
    }

    private static Product Product(string id, int inventory) => new()
    {
        Id = id,
        NameAr = "منتج",
        NameEn = "Product",
        Price = 10,
        Category = "HotDrinks",
        Inventory = inventory,
        MaxInventory = 100
    };

    private sealed class FakeTicketConfigStore : ITicketConfigStore
    {
        public bool EnforceInventoryTracking { get; set; } = true;
        public string ConfigPath => string.Empty;
        public TicketPrintConfig Load() => new() { EnforceInventoryTracking = EnforceInventoryTracking };
        public void Save(TicketPrintConfig config) => EnforceInventoryTracking = config.EnforceInventoryTracking;
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public User? CurrentUser { get; } = new()
        {
            Username = "admin",
            FullName = "مدير النظام",
            Role = UserRole.Admin,
            IsActive = true
        };

        public bool IsActiveAdmin => true;
    }
}
