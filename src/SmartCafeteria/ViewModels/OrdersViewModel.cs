using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Services;
using SmartCafeteria.Printing;
using System.Collections.ObjectModel;
using System.Windows;

namespace SmartCafeteria.ViewModels;
public partial class OrdersViewModel : ObservableObject
{
    private readonly IOrderService _orderService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly IPrintService _printService;
    private readonly ICategoryService _categoryService;
    private readonly UserSession _userSession;

    [ObservableProperty] private ObservableCollection<Order> _orders = new();
    [ObservableProperty] private Order? _selectedOrder;
    [ObservableProperty] private string _statusFilter = "received";
    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private bool _showReceiptPreview;
    [ObservableProperty] private TicketPrintModel? _receiptPreviewModel;

    public OrdersViewModel(IOrderService orderService, ILogService logService, ISettingsService settingsService, IPrintService printService, ICategoryService categoryService, UserSession userSession)
    {
        _orderService = orderService;
        _logService = logService;
        _settingsService = settingsService;
        _printService = printService;
        _categoryService = categoryService;
        _userSession = userSession;
    }

    public bool IsAdmin => _userSession.IsAdmin;

    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public async Task LoadOrdersAsync()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        FilterOrders(orders);
    }

    /// <summary>Serializes filter-driven reloads so a slow earlier load cannot
    /// overwrite the results of a newer filter.</summary>
    private async Task LoadOrdersSafeAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Order list refresh failed: {0}", ex);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    partial void OnStatusFilterChanged(string value)
    {
        _ = LoadOrdersSafeAsync();
    }

    [RelayCommand]
    private void StatusFilterChanged(string filter)
    {
        if (!string.IsNullOrEmpty(filter))
            StatusFilter = filter;
    }

    private void FilterOrders(IEnumerable<Order> orders)
    {
        var filtered = StatusFilter switch
        {
            "received" => orders.Where(o => o.Status == OrderStatus.Received),
            "cancelled" => orders.Where(o => o.Status == OrderStatus.Cancelled),
            _ => orders
        };
        Orders = new ObservableCollection<Order>(filtered);
    }

    [RelayCommand]
    private async Task CancelOrderAsync(Order? order)
    {
        if (order == null) return;
        try
        {
            var updated = await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);
            if (!updated)
            {
                await _logService.AddLogAsync("تعذر إلغاء الطلب: غير موجود", "error");
                return;
            }

            await _logService.AddLogAsync($"تم إلغاء الطلب #{order.Code}", "info");
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await _logService.AddLogAsync($"فشل إلغاء الطلب: {ex.Message}", "error");
        }
    }

    [RelayCommand]
    private async Task PrintReceiptAsync(Order order)
    {
        if (order == null) return;
        if (string.IsNullOrWhiteSpace(Settings.PrinterName))
        {
            await _logService.AddLogAsync("تعذر الطباعة: لم يتم اختيار طابعة من الإعدادات", "error");
            return;
        }

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

    [RelayCommand]
    private void PreviewReceipt(Order order)
    {
        SelectedOrder = order;
        ReceiptPreviewModel = PrintReceiptBuilder.BuildReceipt(order, Settings);
        ShowReceiptPreview = true;
    }

    [RelayCommand]
    private async Task DeleteOrderAsync(Order order)
    {
        if (order == null || !_userSession.IsAdmin) return;
        if (!Confirmations.Delete($"الطلب #{order.Code} نهائياً (سيُعاد المخزون)")) return;

        var deleted = await _orderService.DeleteOrderAsync(order.Id);
        if (deleted)
            await _logService.AddLogAsync($"تم حذف الطلب #{order.Code} نهائياً", "warning");
        await LoadOrdersAsync();
    }

    [RelayCommand]
    private void CloseReceiptPreview()
    {
        ShowReceiptPreview = false;
    }
}
