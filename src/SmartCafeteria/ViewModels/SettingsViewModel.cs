using Cafeteria.RemoteAccess;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Printing;
using System.Collections.ObjectModel;

namespace SmartCafeteria.ViewModels;
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly ICategoryService _categoryService;
    private readonly IPrintService _printService;
    private readonly ITicketConfigStore _ticketConfigStore;
    private readonly ICurrentUserContext _currentUser;
    private readonly ActivationViewModel _activation;
    private readonly IRemoteAccessCoordinator _remoteAccess;

    private readonly SemaphoreSlim _persistGate = new(1, 1);

    private async Task PersistSerializedAsync(Func<Task> persist)
    {
        await _persistGate.WaitAsync();
        try
        {
            await persist();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Settings save failed: {0}", ex);
            StatusMessage = "تعذر حفظ الإعدادات.";
        }
        finally
        {
            _persistGate.Release();
        }
    }

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private ObservableCollection<string> _availablePrinters = new();
    [ObservableProperty] private string _selectedPrinter = string.Empty;
    [ObservableProperty] private Core.Enums.PrintMethod _selectedPrintMethod = Core.Enums.PrintMethod.FullReceiptAndSectionReceipts;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<CategoryRow> _categories = new();

    [ObservableProperty] private int _ticketFontSize = TicketPrintConfig.DefaultFontSize;

    [ObservableProperty] private int _ticketLineSpacing = TicketPrintConfig.DefaultLineSpacing;

    [ObservableProperty] private bool _enforceInventoryTracking = true;

    private bool _suppressTicketFontSizePersist;
    private int _lastKnownGoodTicketFontSize = TicketPrintConfig.DefaultFontSize;

    private bool _suppressTicketLineSpacingPersist;
    private int _lastKnownGoodTicketLineSpacing = TicketPrintConfig.DefaultLineSpacing;

    [ObservableProperty] private string _newCategoryNameAr = string.Empty;
    [ObservableProperty] private string _newCategoryNameEn = string.Empty;
    [ObservableProperty] private Category? _editingCategory;
    [ObservableProperty] private string _editCategoryNameAr = string.Empty;
    [ObservableProperty] private string _editCategoryNameEn = string.Empty;
    [ObservableProperty] private string _categoryMessage = string.Empty;

    [ObservableProperty] private bool _isRemoteBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteNotLinked))]
    private bool _isRemoteLinked;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoteActionText))]
    private bool _isRemoteEnabled;
    [ObservableProperty] private bool _isRemoteCloudConnected;
    [ObservableProperty] private string _remoteStatusText = "غير مرتبط";
    [ObservableProperty] private string _remoteCloudBaseUrl = "https://";
    [ObservableProperty] private string _remoteClaimCode = string.Empty;
    [ObservableProperty] private string _remoteInstallationName = Environment.MachineName;
    [ObservableProperty] private string _remoteMessage = string.Empty;
    [ObservableProperty] private string _remoteInstallationDisplay = string.Empty;
    [ObservableProperty] private string _remoteCloudDisplay = string.Empty;
    [ObservableProperty] private string _remoteLocalApiDisplay = string.Empty;

    public bool IsRemoteNotLinked => !IsRemoteLinked;

    public string RemoteActionText => IsRemoteEnabled ? "إيقاف المزامنة" : "تفعيل المزامنة";

    public SettingsViewModel(
        ISettingsService settingsService,
        ILogService logService,
        ICategoryService categoryService,
        IPrintService printService,
        ITicketConfigStore ticketConfigStore,
        ICurrentUserContext currentUser,
        ActivationViewModel activation,
        IRemoteAccessCoordinator remoteAccess)
    {
        _settingsService = settingsService;
        _logService = logService;
        _categoryService = categoryService;
        _printService = printService;
        _ticketConfigStore = ticketConfigStore;
        _currentUser = currentUser;
        _activation = activation;
        _remoteAccess = remoteAccess;
        _remoteAccess.StatusChanged += OnRemoteStatusChanged;
        ApplyRemoteStatus(_remoteAccess.Status);
        LoadPrinters();
        LoadTicketFontSize();
        LoadTicketLineSpacing();
        LoadInventoryTracking();
    }

    public List<Core.Enums.PrintMethod> PrintMethods { get; } = Enum.GetValues<Core.Enums.PrintMethod>().ToList();

    public ActivationViewModel Activation => _activation;

    public async Task LoadAsync()
    {
        ApplyRemoteStatus(_remoteAccess.Status);
        LoadTicketFontSize();
        LoadTicketLineSpacing();
        LoadInventoryTracking();
        await RefreshCategoriesAsync();
        await Activation.RefreshStatusAsync();
    }

    private void OnRemoteStatusChanged(object? sender, RemoteAccessStatus status)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyRemoteStatus(status);
            return;
        }

        dispatcher.BeginInvoke(() => ApplyRemoteStatus(status));
    }

    private void ApplyRemoteStatus(RemoteAccessStatus status)
    {
        IsRemoteLinked = status.IsLinked;
        IsRemoteEnabled = status.IsEnabled;
        IsRemoteCloudConnected = status.IsCloudConnected;
        RemoteInstallationDisplay = status.InstallationName;
        RemoteCloudDisplay = status.CloudBaseUrl;
        RemoteLocalApiDisplay = string.IsNullOrWhiteSpace(status.LocalApiUrl)
            ? "غير متاحة"
            : status.LocalApiUrl;

        if (status.IsLinked)
        {
            RemoteCloudBaseUrl = status.CloudBaseUrl;
            RemoteInstallationName = status.InstallationName;
        }

        RemoteStatusText = !status.IsLinked
            ? "غير مرتبط"
            : !status.IsEnabled
                ? "المزامنة موقوفة"
                : status.IsCloudConnected
                    ? "متصل بالسحابة"
                    : "غير متصل مؤقتاً";

        if (!string.IsNullOrWhiteSpace(status.LastError))
            RemoteMessage = status.LastError;
    }

    [RelayCommand]
    private async Task LinkRemoteAccessAsync()
    {
        if (IsRemoteBusy)
            return;

        IsRemoteBusy = true;
        RemoteMessage = "جارٍ ربط هذا الجهاز بالسحابة...";

        try
        {
            var result = await _remoteAccess.LinkAsync(new RemoteLinkRequest(
                RemoteCloudBaseUrl,
                RemoteClaimCode,
                RemoteInstallationName));

            ApplyRemoteStatus(result.Status);

            if (result.Success)
            {
                RemoteClaimCode = string.Empty;
                RemoteMessage = "تم ربط الجهاز بالسحابة بنجاح.";
                await _logService.AddLogAsync("تم ربط الجهاز بالإدارة السحابية", "success");
            }
            else
            {
                RemoteMessage = result.Error ?? "تعذر ربط الجهاز بالسحابة.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Remote link failed: {0}", ex);
            RemoteMessage = "تعذر ربط الجهاز بالسحابة.";
        }
        finally
        {
            IsRemoteBusy = false;
        }
    }

    [RelayCommand]
    private async Task UnlinkRemoteAccessAsync()
    {
        if (IsRemoteBusy || !IsRemoteLinked)
            return;

        var confirmed = System.Windows.MessageBox.Show(
            "هل تريد إلغاء ربط هذا الجهاز بالإدارة السحابية؟\nستتوقف أوامر الهاتف والإشعارات عن هذا الجهاز.",
            "تأكيد إلغاء الربط",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;

        if (!confirmed)
            return;

        IsRemoteBusy = true;
        try
        {
            await _remoteAccess.UnlinkAsync();
            ApplyRemoteStatus(_remoteAccess.Status);
            RemoteMessage = "تم إلغاء ربط الجهاز بالسحابة.";
            await _logService.AddLogAsync("تم إلغاء ربط الجهاز بالإدارة السحابية", "warning");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Remote unlink failed: {0}", ex);
            RemoteMessage = "تعذر إلغاء الربط. قد تحتاج إلى تشغيل البرنامج كمسؤول.";
        }
        finally
        {
            IsRemoteBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleRemoteAccessAsync()
    {
        if (IsRemoteBusy || !IsRemoteLinked)
            return;

        var enable = !IsRemoteEnabled;
        IsRemoteBusy = true;

        try
        {
            await _remoteAccess.SetEnabledAsync(enable);
            ApplyRemoteStatus(_remoteAccess.Status);
            RemoteMessage = enable
                ? "تم تفعيل المزامنة السحابية."
                : "تم إيقاف المزامنة السحابية مؤقتاً.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Remote toggle failed: {0}", ex);
            RemoteMessage = "تعذر تغيير حالة المزامنة.";
        }
        finally
        {
            IsRemoteBusy = false;
        }
    }

    private void LoadInventoryTracking()
    {
        try
        {
            EnforceInventoryTracking = _ticketConfigStore.Load().EnforceInventoryTracking;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = "تعذر قراءة إعداد ربط المخزون بالبيع";
        }
    }

    /// <summary>
    /// The ticket font size lives in a JSON config file next to the database,
    /// never in the AppSettings table, so the deployed database schema is untouched.
    /// </summary>
    private void LoadTicketFontSize()
    {
        try
        {
            var config = _ticketConfigStore.Load();
            ApplyTicketFontSizeWithoutPersisting(config.TicketFontSize);
            _lastKnownGoodTicketFontSize = TicketFontSize;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر قراءة حجم خط الإيصال";
        }
    }

    /// <summary>
    /// Sets the slider value without triggering a save. Used while loading the
    /// persisted value and while rolling back a failed save.
    /// </summary>
    private void ApplyTicketFontSizeWithoutPersisting(int value)
    {
        _suppressTicketFontSizePersist = true;
        try
        {
            TicketFontSize = value;
        }
        finally
        {
            _suppressTicketFontSizePersist = false;
        }
    }

    partial void OnTicketFontSizeChanged(int value)
    {
        if (_suppressTicketFontSizePersist) return;

        var clamped = TicketPrintConfig.ClampFontSize(value);
        if (clamped != value)
        {
            // The slider can only produce supported sizes; reflect the clamped
            // value back so the view never shows a size that was not stored.
            ApplyTicketFontSizeWithoutPersisting(clamped);
        }

        try
        {
            var config = _ticketConfigStore.Load();
            config.TicketFontSize = clamped;
            _ticketConfigStore.Save(config);
            _lastKnownGoodTicketFontSize = clamped;
            StatusMessage = $"تم ضبط حجم خط الإيصال: {clamped}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر حفظ حجم الخط";
            // Put the slider back on the value that is actually stored.
            ApplyTicketFontSizeWithoutPersisting(_lastKnownGoodTicketFontSize);
        }
    }

    /// <summary>
    /// The ticket line spacing lives in the same JSON config file as the font
    /// size, never in the AppSettings table, so the deployed database schema is
    /// untouched.
    /// </summary>
    private void LoadTicketLineSpacing()
    {
        try
        {
            var config = _ticketConfigStore.Load();
            ApplyTicketLineSpacingWithoutPersisting(config.TicketLineSpacing);
            _lastKnownGoodTicketLineSpacing = TicketLineSpacing;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر قراءة تباعد أسطر الإيصال";
        }
    }

    /// <summary>
    /// Sets the slider value without triggering a save. Used while loading the
    /// persisted value and while rolling back a failed save.
    /// </summary>
    private void ApplyTicketLineSpacingWithoutPersisting(int value)
    {
        _suppressTicketLineSpacingPersist = true;
        try
        {
            TicketLineSpacing = value;
        }
        finally
        {
            _suppressTicketLineSpacingPersist = false;
        }
    }

    partial void OnTicketLineSpacingChanged(int value)
    {
        if (_suppressTicketLineSpacingPersist) return;

        var clamped = TicketPrintConfig.ClampLineSpacing(value);
        if (clamped != value)
        {
            // The slider can only produce supported values; reflect the clamped
            // value back so the view never shows a spacing that was not stored.
            ApplyTicketLineSpacingWithoutPersisting(clamped);
        }

        try
        {
            var config = _ticketConfigStore.Load();
            config.TicketLineSpacing = clamped;
            _ticketConfigStore.Save(config);
            _lastKnownGoodTicketLineSpacing = clamped;
            StatusMessage = $"تم ضبط تباعد أسطر الإيصال: {clamped}%";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر حفظ تباعد الأسطر";
            // Put the slider back on the value that is actually stored.
            ApplyTicketLineSpacingWithoutPersisting(_lastKnownGoodTicketLineSpacing);
        }
    }

    private async Task RefreshCategoriesAsync()
    {
        var categories = (await _categoryService.GetAllAsync()).ToList();
        var counts = await _categoryService.GetProductCountsAsync();
        Categories = new ObservableCollection<CategoryRow>(
            categories.Select(c => new CategoryRow(
                c,
                counts.TryGetValue(c.Id, out var count) ? count : 0)));
    }

    private void LoadPrinters()
    {
        var printers = ThermalPrinter.GetInstalledPrinters();
        // Keep showing the saved printer even if it is currently offline/unlisted,
        // so the selection is never silently cleared.
        if (!string.IsNullOrWhiteSpace(Settings.PrinterName) && !printers.Contains(Settings.PrinterName))
            printers.Insert(0, Settings.PrinterName);
        AvailablePrinters = new ObservableCollection<string>(printers);
        SelectedPrinter = Settings.PrinterName ?? string.Empty;
    }

    partial void OnSelectedPrinterChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (Settings.PrinterName == value) return;
        Settings.PrinterName = value;
        _ = PersistSerializedAsync(PersistPrinterAsync);
    }

    partial void OnSettingsChanged(AppSettings value)
    {
        if (value != null)
            SelectedPrintMethod = value.PrintMethod;
    }

    partial void OnSelectedPrintMethodChanged(Core.Enums.PrintMethod value)
    {
        if (Settings.PrintMethod == value) return;
        Settings.PrintMethod = value;
        _ = PersistSerializedAsync(PersistPrintMethodAsync);
    }

    private async Task PersistPrintMethodAsync()
    {
        try
        {
            await _settingsService.SaveSettingsAsync(Settings);
            StatusMessage = "تم ضبط طريقة الطباعة";
            await _logService.AddLogAsync("تم ضبط طريقة الطباعة", "success");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر حفظ طريقة الطباعة";
        }
    }

    private async Task PersistPrinterAsync()
    {
        try
        {
            await _settingsService.SaveSettingsAsync(Settings);
            StatusMessage = $"تم ضبط الطابعة: {Settings.PrinterName}";
            await _logService.AddLogAsync($"تم ضبط الطابعة: {Settings.PrinterName}", "success");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"تعذر حفظ الطابعة";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveSettingsAsync(Settings);
            var config = _ticketConfigStore.Load();
            config.EnforceInventoryTracking = EnforceInventoryTracking;
            _ticketConfigStore.Save(config);
            LoadPrinters();
            await _logService.AddLogAsync("تم حفظ الإعدادات", "success");
            StatusMessage = "تم حفظ الإعدادات بنجاح";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = "تعذر حفظ الإعدادات.";
        }
    }

    [RelayCommand]
    private void RefreshPrinters()
    {
        LoadPrinters();
    }

    [RelayCommand]
    private async Task TestPrintAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.PrinterName))
        {
            StatusMessage = "الرجاء اختيار طابعة أولاً ثم حفظ الإعدادات";
            return;
        }

        try
        {
            var sample = new Order
            {
                Id = "test-print",
                Code = "TEST",
                Timestamp = DateTime.Now,
                PaymentMethod = Core.Enums.PaymentMethod.Cash,
                StationId = Environment.MachineName,
                CashierName = _currentUser.CurrentUser?.DisplayName ?? string.Empty,
                Status = Core.Enums.OrderStatus.Received,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductNameAr = "شاي لبن", ProductPrice = 800, Quantity = 1, ProductCategory = "HotDrinks" },
                    new OrderItem { ProductNameAr = "عصير فراولة", ProductPrice = 2500, Quantity = 2, ProductCategory = "Juices" },
                },
            };
            sample.Subtotal = sample.Items.Sum(i => i.ProductPrice * i.Quantity);
            sample.Total = sample.Subtotal;
            var categoryNames = (await _categoryService.GetAllAsync())
                .ToDictionary(c => c.Id, c => c.NameAr);
            await Printing.PrintReceiptBuilder.PrintOrderWithMethodAsync(
                _printService, sample, Settings, categoryNames);
            StatusMessage = "تم إرسال صفحة التجربة إلى الطابعة بنجاح";
            await _logService.AddLogAsync("تمت طباعة صفحة تجريبية", "success");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"فشلت التجربة";
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            await _logService.AddLogAsync($"فشلت طباعة التجربة", "error");
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        CategoryMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewCategoryNameAr))
        {
            CategoryMessage = "الرجاء إدخال اسم الفئة بالعربي";
            return;
        }

        var category = new Category
        {
            NameAr = NewCategoryNameAr.Trim(),
            NameEn = NewCategoryNameEn.Trim()
        };
        try
        {
            await _categoryService.AddAsync(category);
        }
        catch (InvalidOperationException)
        {
            CategoryMessage = "هذه الفئة موجودة بالفعل";
            return;
        }
        await _logService.AddLogAsync($"تمت إضافة فئة جديدة: {category.NameAr}", "success");
        CategoryMessage = "تمت إضافة الفئة بنجاح";
        NewCategoryNameAr = string.Empty;
        NewCategoryNameEn = string.Empty;
        await RefreshCategoriesAsync();
    }

    [RelayCommand]
    private void StartEditCategory(CategoryRow row)
    {
        if (row == null) return;
        EditingCategory = row.Category;
        EditCategoryNameAr = row.Category.NameAr;
        EditCategoryNameEn = row.Category.NameEn;
        CategoryMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelCategoryEdit()
    {
        EditingCategory = null;
    }

    [RelayCommand]
    private async Task SaveCategoryEditAsync()
    {
        if (EditingCategory == null) return;
        if (string.IsNullOrWhiteSpace(EditCategoryNameAr))
        {
            CategoryMessage = "الرجاء إدخال اسم الفئة بالعربي";
            return;
        }

        EditingCategory.NameAr = EditCategoryNameAr.Trim();
        EditingCategory.NameEn = EditCategoryNameEn.Trim();
        await _categoryService.UpdateAsync(EditingCategory);
        await _logService.AddLogAsync($"تم تحديث الفئة: {EditingCategory.NameAr}", "info");
        CategoryMessage = "تم حفظ التعديلات بنجاح";
        EditingCategory = null;
        await RefreshCategoriesAsync();
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(string categoryId)
    {
        if (string.IsNullOrEmpty(categoryId)) return;
        var row = Categories.FirstOrDefault(c => c.Category.Id == categoryId);
        if (!Confirmations.Delete($"الفئة {row?.Category.NameAr ?? categoryId}")) return;
        var ok = await _categoryService.DeleteAsync(categoryId);
        if (!ok)
        {
            CategoryMessage = "لا يمكن حذف الفئة لأنها مستخدمة في منتجات";
            return;
        }
        await _logService.AddLogAsync($"تم حذف الفئة: {row?.Category.NameAr ?? categoryId}", "warning");
        CategoryMessage = "تم حذف الفئة";
        if (EditingCategory?.Id == categoryId)
            EditingCategory = null;
        await RefreshCategoriesAsync();
    }
}

public sealed class CategoryRow
{
    public CategoryRow(Category category, int productCount)
    {
        Category = category;
        ProductCount = productCount;
    }

    public Category Category { get; }
    public int ProductCount { get; }
}
