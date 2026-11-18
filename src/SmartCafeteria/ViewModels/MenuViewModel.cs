using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SmartCafeteria.ViewModels;
public partial class MenuViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ILogService _logService;
    private readonly ICategoryService _categoryService;
    private readonly ITicketConfigStore _ticketConfigStore;
    private List<Product> _allProducts = new();

    [ObservableProperty] private ObservableCollection<Product> _products = new();
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedCategory = "all";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private bool _showCustomizationDialog;
    [ObservableProperty] private List<CustomizationOption> _selectedCustomizations = new();
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private bool _enforceInventoryTracking = true;

    public ObservableCollection<CartItem> Cart { get; } = new();

    public MenuViewModel(IProductService productService, ILogService logService, ICategoryService categoryService, ITicketConfigStore ticketConfigStore)
    {
        _productService = productService;
        _logService = logService;
        _categoryService = categoryService;
        _ticketConfigStore = ticketConfigStore;

        Cart.CollectionChanged += OnCartCollectionChanged;
        HookCartItems();
    }

    private void OnCartCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (CartItem item in e.NewItems)
                item.PropertyChanged += CartItem_PropertyChanged;
        if (e.OldItems != null)
            foreach (CartItem item in e.OldItems)
                item.PropertyChanged -= CartItem_PropertyChanged;

        RaiseCartTotalsChanged();
    }

    private void HookCartItems()
    {
        foreach (var item in Cart)
            item.PropertyChanged += CartItem_PropertyChanged;
    }

    private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity))
            RaiseCartTotalsChanged();
    }

    private void RaiseCartTotalsChanged()
    {
        OnPropertyChanged(nameof(CartSubtotal));
        OnPropertyChanged(nameof(CartCustomizationsTotal));
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartCount));
    }

    public int CartCount => Cart.Sum(c => c.Quantity);

    public async Task LoadProductsAsync()
    {
        EnforceInventoryTracking = _ticketConfigStore.Load().EnforceInventoryTracking;
        Categories = new ObservableCollection<Category>(await _categoryService.GetAllAsync());
        if (SelectedCategory != "all" && !Categories.Any(c => c.Id == SelectedCategory))
            SelectedCategory = "all";
        _allProducts = (await _productService.GetAllProductsAsync()).ToList();
        FilterProducts();
    }

    partial void OnSearchQueryChanged(string value) => FilterProducts();
    partial void OnSelectedCategoryChanged(string value) => FilterProducts();

    [RelayCommand]
    private void CategoryChanged(string category)
    {
        if (!string.IsNullOrEmpty(category))
            SelectedCategory = category;
    }

    private void FilterProducts()
    {
        var filtered = _allProducts.AsEnumerable();

        if (SelectedCategory != "all")
        {
            filtered = filtered.Where(p => p.Category == SelectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            filtered = filtered.Where(p =>
                p.NameAr.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.NameEn.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode != null && p.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        Products = new ObservableCollection<Product>(filtered);
    }

    [RelayCommand]
    private void QuickAdd(Product product)
    {
        if (product == null || (EnforceInventoryTracking && product.Inventory <= 0)) return;

        var cartItem = new CartItem
        {
            CartId = product.Id,
            Product = product,
            Quantity = 1,
            SelectedCustomizations = new List<CustomizationOption>(),
            Notes = string.Empty
        };

        var existing = Cart.FirstOrDefault(c => c.CartId == cartItem.CartId);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            Cart.Add(cartItem);
        }
    }

    [RelayCommand]
    private void OpenCustomization(Product product)
    {
        if (product == null || (EnforceInventoryTracking && product.Inventory <= 0)) return;
        SelectedProduct = product;
        SelectedCustomizations = new List<CustomizationOption>();
        Notes = string.Empty;
        Quantity = 1;
        ShowCustomizationDialog = true;
    }

    [RelayCommand]
    private void CloseCustomization()
    {
        ShowCustomizationDialog = false;
        SelectedProduct = null;
    }

    [RelayCommand]
    private void ToggleCustomization(CustomizationOption option)
    {
        if (SelectedCustomizations.Any(c => c.Id == option.Id))
            SelectedCustomizations.Remove(option);
        else
            SelectedCustomizations.Add(option);
    }

    [RelayCommand]
    private void AddToCart()
    {
        if (SelectedProduct == null) return;

        var cartItem = new CartItem
        {
            CartId = $"{SelectedProduct.Id}-{string.Join(",", SelectedCustomizations.Select(c => c.Id))}-{Notes}",
            Product = SelectedProduct,
            Quantity = Quantity,
            SelectedCustomizations = new List<CustomizationOption>(SelectedCustomizations),
            Notes = Notes
        };

        var existing = Cart.FirstOrDefault(c => c.CartId == cartItem.CartId);
        if (existing != null)
        {
            existing.Quantity += Quantity;
        }
        else
        {
            Cart.Add(cartItem);
        }

        ShowCustomizationDialog = false;
        SelectedProduct = null;
    }

    [RelayCommand]
    private void QuantityUp()
    {
        if (Quantity < 99)
            Quantity++;
    }

    [RelayCommand]
    private void QuantityDown()
    {
        if (Quantity > 1)
            Quantity--;
    }

    [RelayCommand]
    private void RemoveFromCart(CartItem item)
    {
        Cart.Remove(item);
    }

    [RelayCommand]
    private void IncrementQuantity(CartItem item)
    {
        item.Quantity++;
    }

    [RelayCommand]
    private void DecrementQuantity(CartItem item)
    {
        if (item.Quantity > 1)
            item.Quantity--;
    }

    public decimal CartSubtotal => Cart.Sum(c => c.Product.Price * c.Quantity);
    public decimal CartCustomizationsTotal => Cart.Sum(c => c.SelectedCustomizations.Sum(s => s.Price) * c.Quantity);
    public decimal CartTotal => CartSubtotal + CartCustomizationsTotal;

    [RelayCommand]
    private void ClearCart()
    {
        Cart.Clear();
    }
}