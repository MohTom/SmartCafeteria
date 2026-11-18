using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SmartCafeteria.ViewModels;
public partial class CartViewModel : ObservableObject
{
    private readonly IOrderService _orderService;
    private readonly ILogService _logService;
    private readonly IPrintService _printService;
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty] private ObservableCollection<CartItem> _cartItems = new();
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private OrderType _selectedOrderType = OrderType.Local;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private bool _isPreOrder;
    [ObservableProperty] private string _pickupTime = "12:30";
    [ObservableProperty] private AppSettings _settings = new();

    public decimal Subtotal => CartItems.Sum(c => c.Product.Price * c.Quantity);
    public decimal CustomizationsTotal => CartItems.Sum(c => c.SelectedCustomizations.Sum(s => s.Price) * c.Quantity);
    public decimal Total => Subtotal + CustomizationsTotal;
    public int CartCount => CartItems.Sum(c => c.Quantity);
    public List<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>().ToList();
    public List<OrderType> OrderTypes { get; } = Enum.GetValues<OrderType>().ToList();

    public CartViewModel(IOrderService orderService, ILogService logService, IPrintService printService, ICategoryService categoryService, ICurrentUserContext currentUser)
    {
        _orderService = orderService;
        _logService = logService;
        _printService = printService;
        _categoryService = categoryService;
        _currentUser = currentUser;

        CartItems.CollectionChanged += OnCartCollectionChanged;
        HookCartItems();
    }

    public void SetCartItems(ObservableCollection<CartItem> items)
    {
        if (CartItems == items) return;

        CartItems.CollectionChanged -= OnCartCollectionChanged;
        foreach (var item in CartItems)
            item.PropertyChanged -= CartItem_PropertyChanged;

        CartItems = items;

        CartItems.CollectionChanged += OnCartCollectionChanged;
        HookCartItems();
        RaiseTotalsChanged();
    }

    private void OnCartCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (CartItem item in e.NewItems)
                item.PropertyChanged += CartItem_PropertyChanged;
        if (e.OldItems != null)
            foreach (CartItem item in e.OldItems)
                item.PropertyChanged -= CartItem_PropertyChanged;

        RaiseTotalsChanged();
    }

    private void HookCartItems()
    {
        foreach (var item in CartItems)
            item.PropertyChanged += CartItem_PropertyChanged;
    }

    private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity))
            RaiseTotalsChanged();
    }

    private void RaiseTotalsChanged()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(CustomizationsTotal));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CartCount));
    }

    [RelayCommand]
    private async Task PlaceOrderAsync()
    {
        if (CartItems.Count == 0) return;

        var order = await _orderService.CreateOrderAsync(
            CartItems.ToList(),
            SelectedPaymentMethod,
            Environment.MachineName,
            _currentUser.CurrentUser?.DisplayName ?? string.Empty,
            IsPreOrder,
            IsPreOrder ? PickupTime : null,
            SelectedOrderType,
            Notes.Trim());

        await _logService.AddLogAsync($"تم إنشاء الطلب #{order.Code} - الإجمالي: {order.Total:F2} {Settings.CurrencyAr}", "success");

        // Print immediately (no receipt modal) then clear the cart.
        if (string.IsNullOrWhiteSpace(Settings.PrinterName))
        {
            await _logService.AddLogAsync("تم حفظ الطلب بدون طباعة: لم يتم اختيار طابعة", "warning");
        }
        else
        {
            try
            {
                var categoryNames = (await _categoryService.GetAllAsync())
                    .ToDictionary(c => c.Id, c => c.NameAr);
                await Printing.PrintReceiptBuilder.PrintOrderWithMethodAsync(
                    _printService, order, Settings, categoryNames);
                await _logService.AddLogAsync($"تم طباعة الإيصال للطلب #{order.Code}", "success");
            }
            catch (Exception ex)
            {
                await _logService.AddLogAsync($"فشلت الطباعة: {ex.Message}", "error");
            }
        }

        CartItems.Clear();
        SelectedOrderType = OrderType.Local;
        Notes = string.Empty;
    }
}