using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SmartCafeteria.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly ISettingsService _settingsService;
    private readonly IPdfService _pdfReportService;
    private readonly ILogService _logService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsPdfService _analyticsPdfService;

    private AnalyticsBundle? _analyticsBundle;
    private DateTime? _bundleStart;
    private DateTime? _bundleEnd;
    private bool _ready;
    private bool _applyingPreset;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    /// <summary>The reload that follows a change of the date fields. A caller can wait for
    /// it to know that the screen already shows the new range.</summary>
    internal Task PendingReload { get; private set; } = Task.CompletedTask;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private decimal _totalRevenue;
    [ObservableProperty] private int _totalOrders;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate = DateTime.Today;
    [ObservableProperty] private string _selectedPeriodLabel = "الكل";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<SystemLog> _recentLogs = new();
    [ObservableProperty] private ObservableCollection<AnalyticsReportButton> _analyticsButtons = new();

    public ReportsViewModel(IOrderService orderService, IProductService productService, ISettingsService settingsService, IPdfService pdfReportService, ILogService logService, IAnalyticsService analyticsService, IAnalyticsPdfService analyticsPdfService)
    {
        _orderService = orderService;
        _productService = productService;
        _settingsService = settingsService;
        _pdfReportService = pdfReportService;
        _logService = logService;
        _analyticsService = analyticsService;
        _analyticsPdfService = analyticsPdfService;
        _ready = true;
    }

    public async Task LoadStatsAsync()
    {
        var orders = (await _orderService.GetAllOrdersAsync()).ToList();
        var (start, end) = NormalizeRange(StartDate, EndDate);
        var inRange = orders.Where(o =>
            (!start.HasValue || o.Timestamp >= start.Value) &&
            (!end.HasValue || o.Timestamp <= end.Value) &&
            o.Status == OrderStatus.Received).ToList();

        TotalRevenue = inRange.Sum(o => o.Total);
        TotalOrders = inRange.Count;

        var products = (await _productService.GetAllProductsAsync()).ToList();
        LowStockCount = products.Count(p => p.Inventory <= Settings.LowStockThreshold);

        var logs = (await _logService.GetAllLogsAsync()).ToList();
        var logsInRange = logs.Where(l =>
            (!start.HasValue || l.Timestamp >= start.Value) &&
            (!end.HasValue || l.Timestamp <= end.Value))
            .OrderByDescending(l => l.Timestamp)
            .Take(100).ToList();
        RecentLogs = new ObservableCollection<SystemLog>(logsInRange);

        try
        {
            var bundle = await BuildBundleAsync();
            Settings = await _settingsService.GetSettingsAsync();
            AnalyticsButtons = new ObservableCollection<AnalyticsReportButton>(
                (bundle?.Reports ?? Array.Empty<AnalyticsReport>())
                    .Select(r => new AnalyticsReportButton(r.Kind, r.Title)));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Analytics bundle build failed: {0}", ex);
            StatusMessage = "تعذر تحديث التقارير التحليلية.";
            _analyticsBundle = null;
            _bundleStart = null;
            _bundleEnd = null;
            AnalyticsButtons = new ObservableCollection<AnalyticsReportButton>();
        }
    }

    /// <summary>Returns the analytics bundle for the range the date fields hold right now.
    /// The bundle is rebuilt whenever that range changed, so a report never carries a
    /// range the date fields no longer show.</summary>
    private async Task<AnalyticsBundle?> BuildBundleAsync()
    {
        var start = StartDate;
        var end = EndDate;

        if (_analyticsBundle != null && _bundleStart == start && _bundleEnd == end)
            return _analyticsBundle;

        var bundle = await _analyticsService.BuildAsync(start, end);
        _analyticsBundle = bundle;
        _bundleStart = start;
        _bundleEnd = end;
        return bundle;
    }

    partial void OnStartDateChanged(DateTime? value) => QueueReload();

    partial void OnEndDateChanged(DateTime? value) => QueueReload();

    /// <summary>Reloads the screen after the date fields changed. The reload waits for the
    /// previous one, so two quick changes cannot leave an older range on the screen.</summary>
    private void QueueReload()
    {
        if (!_ready || _applyingPreset) return;

        SelectedPeriodLabel = StartDate.HasValue || EndDate.HasValue ? "مخصص" : "الكل";
        PendingReload = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _reloadGate.WaitAsync();
        try
        {
            await LoadStatsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private static (DateTime? Start, DateTime? End) NormalizeRange(DateTime? startDate, DateTime? endDate)
    {
        DateTime? start = startDate?.Date;
        DateTime? end = endDate?.Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }

    [RelayCommand]
    private async Task ApplyPeriodAsync(string period)
    {
        _applyingPreset = true;
        try
        {
            var today = DateTime.Today;
            switch (period)
            {
                case "daily":
                    StartDate = today;
                    EndDate = today;
                    SelectedPeriodLabel = "يومي";
                    break;
                case "weekly":
                    StartDate = today.AddDays(-6);
                    EndDate = today;
                    SelectedPeriodLabel = "أسبوعي";
                    break;
                case "monthly":
                    StartDate = new DateTime(today.Year, today.Month, 1);
                    EndDate = today;
                    SelectedPeriodLabel = "شهري";
                    break;
                default:
                    StartDate = null;
                    EndDate = null;
                    SelectedPeriodLabel = "الكل";
                    break;
            }
        }
        finally
        {
            _applyingPreset = false;
        }

        PendingReload = RefreshAsync();
        await PendingReload;
    }

    [RelayCommand]
    private async Task DownloadSalesReportAsync()
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync();
            var filePath = await _pdfReportService.GenerateSalesReportAsync(orders, Settings, StartDate, EndDate);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    [RelayCommand]
    private async Task DownloadOrdersReportAsync()
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync();
            var filePath = await _pdfReportService.GenerateOrdersReportAsync(orders, Settings, StartDate, EndDate);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    [RelayCommand]
    private async Task DownloadInventoryReportAsync()
    {
        try
        {
            var products = await _productService.GetAllProductsAsync();
            var filePath = await _pdfReportService.GenerateInventoryReportAsync(products, Settings);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    [RelayCommand]
    private async Task DownloadOperationsLogReportAsync()
    {
        try
        {
            var logs = await _logService.GetAllLogsAsync();
            var filePath = await _pdfReportService.GenerateOperationsLogReportAsync(logs, Settings, StartDate, EndDate);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    [RelayCommand]
    private async Task DownloadAnalyticsReportAsync(AnalyticsReportKind kind)
    {
        try
        {
            var bundle = await BuildBundleAsync();
            if (bundle == null) return;
            var settings = await _settingsService.GetSettingsAsync();
            var filePath = await _analyticsPdfService.GenerateAnalyticsReportAsync(bundle, kind, settings);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    [RelayCommand]
    private async Task DownloadFinancialReportAsync()
    {
        try
        {
            var bundle = await BuildBundleAsync();
            if (bundle?.Financial == null) return;
            var settings = await _settingsService.GetSettingsAsync();
            var filePath = await _analyticsPdfService.GenerateFinancialReportAsync(bundle.Financial, settings);
            StatusMessage = $"تم حفظ التقرير المالي الشامل: {filePath}";
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ";
        }
    }

    public sealed record AnalyticsReportButton(AnalyticsReportKind Kind, string Title);
}
