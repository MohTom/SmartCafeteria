using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace SmartCafeteria.ViewModels;
public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IProductService _productService;
    private readonly UserSession _userSession;

    [ObservableProperty] private ObservableObject _currentViewModel = null!;
    [ObservableProperty] private string _currentViewName = "القائمة";
    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private ObservableCollection<SystemLog> _logs = new();
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private bool _showNotification;
    [ObservableProperty] private string _currentUserName = string.Empty;
    [ObservableProperty] private string _currentStationName = Environment.MachineName;
    [ObservableProperty] private Brush _notificationBackground = new SolidColorBrush(Color.FromRgb(0x1E, 0x7A, 0x46));

    public event EventHandler? LogoutRequested;

    public MenuViewModel MenuViewModel { get; }
    public CartViewModel CartViewModel { get; }
    public OrdersViewModel OrdersViewModel { get; }
    public InventoryViewModel InventoryViewModel { get; }
    public ReportsViewModel ReportsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public UserManagementViewModel UserManagementViewModel { get; }
    public AccountingViewModel AccountingViewModel { get; }
    public SuppliersViewModel SuppliersViewModel { get; }
    public EmployeesViewModel EmployeesViewModel { get; }
    public ActivationViewModel Activation { get; }

    public bool IsAdmin => _userSession.IsAdmin;

    public MainViewModel(
        ISettingsService settingsService,
        ILogService logService,
        IProductService productService,
        UserSession userSession,
        MenuViewModel menuViewModel,
        CartViewModel cartViewModel,
        OrdersViewModel ordersViewModel,
        InventoryViewModel inventoryViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel,
        UserManagementViewModel userManagementViewModel,
        AccountingViewModel accountingViewModel,
        SuppliersViewModel suppliersViewModel,
        EmployeesViewModel employeesViewModel,
        ActivationViewModel activation)
    {
        _settingsService = settingsService;
        _logService = logService;
        _productService = productService;
        _userSession = userSession;

        MenuViewModel = menuViewModel;
        CartViewModel = cartViewModel;
        OrdersViewModel = ordersViewModel;
        InventoryViewModel = inventoryViewModel;
        ReportsViewModel = reportsViewModel;
        SettingsViewModel = settingsViewModel;
        UserManagementViewModel = userManagementViewModel;
        AccountingViewModel = accountingViewModel;
        SuppliersViewModel = suppliersViewModel;
        EmployeesViewModel = employeesViewModel;
        Activation = activation;

        // POS: menu cart is the single shared cart used by checkout
        CartViewModel.SetCartItems(MenuViewModel.Cart);

        CurrentUserName = _userSession.CurrentUser?.FullName ?? string.Empty;

        CurrentViewModel = MenuViewModel;
    }

    public void RefreshIsAdmin() => OnPropertyChanged(nameof(IsAdmin));

    /// <summary>Must be awaited from the window's Loaded handler so startup
    /// cannot overlap a user navigation.</summary>
    public async Task InitializeAsyncSafe()
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Initialization failed: {0}", ex);
            try { await _logService.AddLogAsync($"خطأ أثناء التهيئة: {ex.Message}", "error"); }
            catch (Exception logError)
            {
                System.Diagnostics.Trace.TraceWarning("Failed to log initialization error: {0}", logError);
            }
        }
    }

    private async Task InitializeAsync()
    {
        Settings = await _settingsService.GetSettingsAsync();
        var logs = await _logService.GetAllLogsAsync();
        Logs = new ObservableCollection<SystemLog>(logs);
        RefreshIsAdmin();

        MenuViewModel.Settings = Settings;
        CartViewModel.Settings = Settings;
        OrdersViewModel.Settings = Settings;
        InventoryViewModel.Settings = Settings;
        ReportsViewModel.Settings = Settings;
        SettingsViewModel.Settings = Settings;
        AccountingViewModel.Settings = Settings;
        SuppliersViewModel.Settings = Settings;
        EmployeesViewModel.Settings = Settings;

        await MenuViewModel.LoadProductsAsync();
        await OrdersViewModel.LoadOrdersAsync();
        await InventoryViewModel.LoadProductsAsync();
        await AccountingViewModel.LoadAsync();
        await SuppliersViewModel.LoadAsync();
        await EmployeesViewModel.LoadAsync();
        await SettingsViewModel.LoadAsync();
        await Activation.RefreshStatusAsync();
    }

    public Task RefreshLicenseStatusAsync() => Activation.RefreshStatusAsync();

    [RelayCommand]
    private async Task NavigateAsync(string viewName)
    {
        // Cashiers are limited to menu, orders and inventory.
        if (!IsAdmin && (viewName == "التقارير" || viewName == "المحاسبة" ||
            viewName == "الموردون" || viewName == "الموظفون" ||
            viewName == "المستخدمون" || viewName == "الإعدادات"))
            return;

        CurrentViewName = viewName;
        CurrentViewModel = viewName switch
        {
            "القائمة" => MenuViewModel,
            "سلة المشتريات" => CartViewModel,
            "الطلبات" => OrdersViewModel,
            "المخزون" => InventoryViewModel,
            "التقارير" => ReportsViewModel,
            "المحاسبة" => AccountingViewModel,
            "الموردون" => SuppliersViewModel,
            "الموظفون" => EmployeesViewModel,
            "المستخدمون" => UserManagementViewModel,
            "الإعدادات" => SettingsViewModel,
            _ => MenuViewModel
        };
        await RefreshCurrentScreenAsync();
    }

    /// <summary>Reloads the target screen (and shared settings) so additions made
    /// elsewhere appear immediately without restarting the app.</summary>
    private async Task RefreshCurrentScreenAsync()
    {
        try
        {
            Settings = await _settingsService.GetSettingsAsync();
            MenuViewModel.Settings = Settings;
            CartViewModel.Settings = Settings;
            OrdersViewModel.Settings = Settings;
            InventoryViewModel.Settings = Settings;
            ReportsViewModel.Settings = Settings;
            SettingsViewModel.Settings = Settings;
            AccountingViewModel.Settings = Settings;
            SuppliersViewModel.Settings = Settings;
            EmployeesViewModel.Settings = Settings;
            RefreshIsAdmin();

            switch (CurrentViewName)
            {
                case "القائمة": await MenuViewModel.LoadProductsAsync(); break;
                case "الطلبات": await OrdersViewModel.LoadOrdersAsync(); break;
                case "المخزون": await InventoryViewModel.LoadProductsAsync(); break;
                case "التقارير": await ReportsViewModel.LoadStatsAsync(); break;
                case "المحاسبة": await AccountingViewModel.LoadAsync(); break;
                case "الموردون": await SuppliersViewModel.LoadAsync(); break;
                case "الموظفون": await EmployeesViewModel.LoadAsync(); break;
                case "المستخدمون": await UserManagementViewModel.LoadUsersAsync(); break;
                case "الإعدادات": await SettingsViewModel.LoadAsync(); break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Screen refresh failed: {0}", ex);
            try { await _logService.AddLogAsync($"خطأ أثناء تحديث الشاشة: {ex.Message}", "error"); }
            catch (Exception logError)
            {
                System.Diagnostics.Trace.TraceWarning("Failed to log screen refresh error: {0}", logError);
            }
        }
    }

    private CancellationTokenSource? _notificationCts;

    public async Task ShowNotificationAsync(string message, string type = "success")
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _notificationCts, cts);
        previous?.Cancel();

        NotificationMessage = message;
        NotificationBackground = type switch
        {
            "error" => new SolidColorBrush(Color.FromRgb(0xD6, 0x32, 0x32)),
            "warning" => new SolidColorBrush(Color.FromRgb(0xE8, 0x88, 0x0C)),
            "info" => new SolidColorBrush(Color.FromRgb(0x1E, 0x6F, 0xA8)),
            _ => new SolidColorBrush(Color.FromRgb(0x1E, 0x7A, 0x46)),
        };
        ShowNotification = true;

        try
        {
            await Task.Delay(3000, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer notification owns the banner now; do not clear it.
            return;
        }

        if (ReferenceEquals(_notificationCts, cts))
            ShowNotification = false;
    }

    [RelayCommand]
    private void Logout()
    {
        _userSession.Logout();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task RefreshLogsAsync()
    {
        var logs = await _logService.GetAllLogsAsync();
        Logs = new ObservableCollection<SystemLog>(logs);
    }
}
