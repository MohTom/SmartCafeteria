using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;
public partial class InventoryViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ILogService _logService;
    private readonly ICategoryService _categoryService;
    private readonly ITicketConfigStore _ticketConfigStore;
    private List<Product> _allProducts = new();

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private Product? _editingProduct;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isAdding;
    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private bool _enforceInventoryTracking = true;

    // New product form
    [ObservableProperty] private string _newNameAr = string.Empty;
    [ObservableProperty] private string _newNameEn = string.Empty;
    [ObservableProperty] private string _newDescriptionAr = string.Empty;
    [ObservableProperty] private string _newDescriptionEn = string.Empty;
    [ObservableProperty] private decimal _newPrice;
    [ObservableProperty] private string _newCategory = string.Empty;
    [ObservableProperty] private int _newInventory;
    [ObservableProperty] private int _newMaxInventory = 100;

    // Edit form
    [ObservableProperty] private string _editNameAr = string.Empty;
    [ObservableProperty] private string _editNameEn = string.Empty;
    [ObservableProperty] private string _editDescriptionAr = string.Empty;
    [ObservableProperty] private string _editDescriptionEn = string.Empty;
    [ObservableProperty] private decimal _editPrice;
    [ObservableProperty] private string _editCategory = string.Empty;
    [ObservableProperty] private int _editInventory;
    [ObservableProperty] private int _editMaxInventory;

    public InventoryViewModel(IProductService productService, ILogService logService, ICategoryService categoryService, ITicketConfigStore ticketConfigStore)
    {
        _productService = productService;
        _logService = logService;
        _categoryService = categoryService;
        _ticketConfigStore = ticketConfigStore;
    }

    public async Task LoadProductsAsync()
    {
        EnforceInventoryTracking = _ticketConfigStore.Load().EnforceInventoryTracking;
        Categories = new ObservableCollection<Category>(await _categoryService.GetAllAsync());
        if (string.IsNullOrEmpty(NewCategory))
            NewCategory = Categories.FirstOrDefault()?.Id ?? string.Empty;
        _allProducts = (await _productService.GetAllProductsAsync()).ToList();
        FilterProducts();
    }

    partial void OnSearchQueryChanged(string value) => FilterProducts();

    private void FilterProducts()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Products = new ObservableCollection<Product>(_allProducts);
        }
        else
        {
            Products = new ObservableCollection<Product>(
                _allProducts.Where(p =>
                    p.NameAr.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.NameEn.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [RelayCommand]
    private void StartAdd()
    {
        ResetNewForm();
        IsAdding = true;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task AddProductAsync()
    {
        var product = new Product
        {
            Id = $"p-custom-{Guid.NewGuid():N}",
            NameAr = NewNameAr,
            NameEn = NewNameEn,
            DescriptionAr = NewDescriptionAr,
            DescriptionEn = NewDescriptionEn,
            Price = NewPrice,
            Category = NewCategory,
            Inventory = EnforceInventoryTracking ? NewInventory : 0,
            MaxInventory = EnforceInventoryTracking ? NewMaxInventory : 100,
            CustomizationOptions = new List<CustomizationOption>()
        };

        await _productService.AddProductAsync(product);
        await _logService.AddLogAsync($"تم إضافة منتج جديد: {product.NameAr}", "success");
        IsAdding = false;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private void StartEdit(Product product)
    {
        EditingProduct = product;
        EditNameAr = product.NameAr;
        EditNameEn = product.NameEn;
        EditDescriptionAr = product.DescriptionAr;
        EditDescriptionEn = product.DescriptionEn;
        EditPrice = product.Price;
        EditCategory = product.Category;
        EditInventory = product.Inventory;
        EditMaxInventory = product.MaxInventory;
        IsEditing = true;
        IsAdding = false;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (EditingProduct == null) return;

        EditingProduct.NameAr = EditNameAr;
        EditingProduct.NameEn = EditNameEn;
        EditingProduct.DescriptionAr = EditDescriptionAr;
        EditingProduct.DescriptionEn = EditDescriptionEn;
        EditingProduct.Price = EditPrice;
        EditingProduct.Category = EditCategory;
        if (EnforceInventoryTracking)
        {
            EditingProduct.Inventory = EditInventory;
            EditingProduct.MaxInventory = EditMaxInventory;
        }

        await _productService.UpdateProductAsync(EditingProduct);
        await _logService.AddLogAsync($"تم تحديث المنتج: {EditingProduct.NameAr}", "info");
        IsEditing = false;
        EditingProduct = null;
        await LoadProductsAsync();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsAdding = false;
        EditingProduct = null;
    }

    [RelayCommand]
    private async Task DeleteProductAsync(string productId)
    {
        var product = Products.FirstOrDefault(p => p.Id == productId);
        if (!Confirmations.Delete($"المنتج {product?.NameAr ?? productId}")) return;
        await _productService.DeleteProductAsync(productId);
        await _logService.AddLogAsync("تم حذف المنتج", "warning");
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task RestockAsync(string productId)
    {
        await _productService.RestockProductAsync(productId);
        await _logService.AddLogAsync("تم تعبئة المنتج", "success");
        await LoadProductsAsync();
    }

    private void ResetNewForm()
    {
        NewNameAr = string.Empty;
        NewNameEn = string.Empty;
        NewDescriptionAr = string.Empty;
        NewDescriptionEn = string.Empty;
        NewPrice = 0;
        NewCategory = Categories.FirstOrDefault()?.Id ?? string.Empty;
        NewInventory = 0;
        NewMaxInventory = 100;
    }
}
