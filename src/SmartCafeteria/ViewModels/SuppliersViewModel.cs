using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;
public partial class SuppliersViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    private readonly ILogService _logService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private decimal _balance;

    [ObservableProperty] private bool _showSupplierDialog;
    [ObservableProperty] private bool _isEditingSupplier;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newAddress = string.Empty;
    [ObservableProperty] private string _newNotes = string.Empty;
    [ObservableProperty] private bool _newIsActive = true;
    [ObservableProperty] private Supplier? _editingSupplier;

    [ObservableProperty] private bool _showPoDialog;
    [ObservableProperty] private Product? _selectedPoProduct;
    [ObservableProperty] private int _poQuantity = 1;
    [ObservableProperty] private decimal _poUnitCost;
    [ObservableProperty] private string _poNotes = string.Empty;

    [ObservableProperty] private bool _showPaymentDialog;
    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private string _paymentNotes = string.Empty;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
    public ObservableCollection<SupplierPayment> Payments { get; } = new();
    public ObservableCollection<PurchaseOrderItem> PoLines { get; } = new();
    public List<Product> Products { get; } = new();
    public List<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>().ToList();

    public SuppliersViewModel(ISupplierService supplierService, IProductService productService, ILogService logService, ICurrentUserContext currentUser)
    {
        _supplierService = supplierService;
        _productService = productService;
        _logService = logService;
        _currentUser = currentUser;
        PoLines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PoTotal));
    }

    public async Task LoadAsync()
    {
        var suppliers = (await _supplierService.GetAllSuppliersAsync()).ToList();
        Suppliers.Clear();
        foreach (var supplier in suppliers)
            Suppliers.Add(supplier);

        Products.Clear();
        foreach (var product in await _productService.GetAllProductsAsync())
            Products.Add(product);

        if (SelectedSupplier != null)
            await SelectSupplierAsync(SelectedSupplier.Id);
    }

    [RelayCommand]
    private async Task SelectSupplierAsync(string supplierId)
    {
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == supplierId);
        if (SelectedSupplier == null) return;

        var orders = (await _supplierService.GetPurchaseOrdersForSupplierAsync(supplierId)).ToList();
        PurchaseOrders.Clear();
        foreach (var order in orders)
            PurchaseOrders.Add(order);

        var payments = (await _supplierService.GetPaymentsForSupplierAsync(supplierId)).ToList();
        Payments.Clear();
        foreach (var payment in payments)
            Payments.Add(payment);

        Balance = await _supplierService.GetSupplierBalanceAsync(supplierId);
    }

    [RelayCommand]
    private void OpenSupplierDialog()
    {
        EditingSupplier = null;
        IsEditingSupplier = false;
        NewName = string.Empty;
        NewPhone = string.Empty;
        NewEmail = string.Empty;
        NewAddress = string.Empty;
        NewNotes = string.Empty;
        NewIsActive = true;
        ShowSupplierDialog = true;
    }

    [RelayCommand]
    private void OpenEditSupplier(Supplier supplier)
    {
        if (supplier == null) return;
        EditingSupplier = supplier;
        IsEditingSupplier = true;
        NewName = supplier.Name;
        NewPhone = supplier.Phone;
        NewEmail = supplier.Email;
        NewAddress = supplier.Address;
        NewNotes = supplier.Notes;
        NewIsActive = supplier.IsActive;
        ShowSupplierDialog = true;
    }

    [RelayCommand]
    private void CloseSupplierDialog()
    {
        ShowSupplierDialog = false;
        EditingSupplier = null;
    }

    [RelayCommand]
    private async Task SaveSupplierAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;

        if (EditingSupplier != null)
        {
            EditingSupplier.Name = NewName.Trim();
            EditingSupplier.Phone = NewPhone.Trim();
            EditingSupplier.Email = NewEmail.Trim();
            EditingSupplier.Address = NewAddress.Trim();
            EditingSupplier.Notes = NewNotes.Trim();
            EditingSupplier.IsActive = NewIsActive;
            await _supplierService.UpdateSupplierAsync(EditingSupplier);
            await _logService.AddLogAsync($"تم تحديث المورد: {EditingSupplier.Name}", "info");
        }
        else
        {
            var supplier = new Supplier
            {
                Id = $"sup-{Guid.NewGuid():N}",
                Name = NewName.Trim(),
                Phone = NewPhone.Trim(),
                Email = NewEmail.Trim(),
                Address = NewAddress.Trim(),
                Notes = NewNotes.Trim(),
                IsActive = NewIsActive,
                CreatedAt = DateTime.Now
            };
            await _supplierService.AddSupplierAsync(supplier);
            await _logService.AddLogAsync($"تم إضافة مورد جديد: {supplier.Name}", "success");
        }

        ShowSupplierDialog = false;
        EditingSupplier = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteSupplierAsync(string supplierId)
    {
        var supplier = Suppliers.FirstOrDefault(s => s.Id == supplierId);
        if (!Confirmations.Delete($"المورد {supplier?.Name ?? supplierId}")) return;
        await _supplierService.DeleteSupplierAsync(supplierId);
        await _logService.AddLogAsync($"تم حذف المورد: {supplier?.Name ?? supplierId}", "warning");
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenPoDialog()
    {
        if (SelectedSupplier == null) return;
        SelectedPoProduct = Products.FirstOrDefault();
        PoQuantity = 1;
        PoUnitCost = 0;
        PoNotes = string.Empty;
        PoLines.Clear();
        ShowPoDialog = true;
    }

    [RelayCommand]
    private void AddPoLine(Product product)
    {
        if (product == null) return;
        var existing = PoLines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += PoQuantity > 0 ? PoQuantity : 1;
            existing.UnitCost = PoUnitCost;
        }
        else
        {
            PoLines.Add(new PurchaseOrderItem
            {
                ProductId = product.Id,
                ProductName = product.NameAr,
                Quantity = PoQuantity > 0 ? PoQuantity : 1,
                UnitCost = PoUnitCost
            });
        }
        PoQuantity = 1;
        PoUnitCost = 0;
        OnPropertyChanged(nameof(PoTotal));
    }

    [RelayCommand]
    private void RemovePoLine(PurchaseOrderItem item)
    {
        PoLines.Remove(item);
        OnPropertyChanged(nameof(PoTotal));
    }

    public decimal PoTotal => PoLines.Sum(l => l.Total);

    [RelayCommand]
    private async Task CreatePoAsync()
    {
        if (SelectedSupplier == null || PoLines.Count == 0) return;

        var order = new PurchaseOrder
        {
            Id = $"po-{Guid.NewGuid():N}",
            Code = _supplierService.GeneratePurchaseOrderCode(),
            SupplierId = SelectedSupplier.Id,
            SupplierName = SelectedSupplier.Name,
            Date = DateTime.Now,
            Status = PurchaseOrderStatus.Pending,
            Notes = PoNotes.Trim(),
            Items = PoLines.ToList()
        };

        await _supplierService.CreatePurchaseOrderAsync(order);
        await _logService.AddLogAsync($"تم إنشاء أمر شراء {order.Code} من {order.SupplierName} - {order.TotalAmount:F2} {Settings.CurrencyAr}", "success");

        ShowPoDialog = false;
        await LoadAsync();
    }

    [RelayCommand]
    private void ClosePoDialog()
    {
        ShowPoDialog = false;
    }

    [RelayCommand]
    private async Task ReceivePoAsync(string purchaseOrderId)
    {
        var order = PurchaseOrders.FirstOrDefault(o => o.Id == purchaseOrderId);
        var userName = _currentUser.CurrentUser?.DisplayName ?? string.Empty;
        var ok = await _supplierService.ReceivePurchaseOrderAsync(purchaseOrderId, userName);
        if (ok)
        {
            await _logService.AddLogAsync($"تم استلام أمر الشراء {order?.Code ?? purchaseOrderId}", "success");
        }
        else
        {
            await _logService.AddLogAsync("تعذر استلام أمر الشراء لأن أحد المنتجات غير موجود", "error");
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CancelPoAsync(string purchaseOrderId)
    {
        var order = PurchaseOrders.FirstOrDefault(o => o.Id == purchaseOrderId);
        await _supplierService.CancelPurchaseOrderAsync(purchaseOrderId);
        await _logService.AddLogAsync($"تم إلغاء أمر الشراء {order?.Code ?? purchaseOrderId}", "warning");
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenPaymentDialog()
    {
        if (SelectedSupplier == null) return;
        PaymentAmount = Balance > 0 ? Balance : 0;
        PaymentMethod = PaymentMethod.Cash;
        PaymentNotes = string.Empty;
        ShowPaymentDialog = true;
    }

    [RelayCommand]
    private void ClosePaymentDialog()
    {
        ShowPaymentDialog = false;
    }

    [RelayCommand]
    private async Task SavePaymentAsync()
    {
        if (SelectedSupplier == null || PaymentAmount <= 0) return;

        var payment = new SupplierPayment
        {
            Id = $"sp-{Guid.NewGuid():N}",
            SupplierId = SelectedSupplier.Id,
            Amount = PaymentAmount,
            Date = DateTime.Now,
            PaymentMethod = PaymentMethod,
            Notes = PaymentNotes.Trim()
        };

        await _supplierService.AddSupplierPaymentAsync(payment);
        await _logService.AddLogAsync($"تم تسجيل دفعة للمورد {SelectedSupplier.Name}: {PaymentAmount:F2} {Settings.CurrencyAr}", "success");

        ShowPaymentDialog = false;
        await LoadAsync();
    }
}