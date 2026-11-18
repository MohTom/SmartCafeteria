using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SmartCafeteria.ViewModels;

/// <summary>Backs the analytical section of the reports screen. It owns its own
/// date filter, defaults to every period, and reads all reports in a single
/// pass so exporting one does not query the database again.</summary>
public partial class ReportsAnalyticsViewModel : ObservableObject
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsPdfService _analyticsPdfService;
    private readonly ISettingsService _settingsService;

    private AnalyticsBundle? _bundle;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;
    [ObservableProperty] private string _periodLabel = "كل الفترات";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<AnalyticsKpi> _kpis = new();
    [ObservableProperty] private ObservableCollection<AnalyticsReportView> _sections = new();

    public ReportsAnalyticsViewModel(
        IAnalyticsService analyticsService,
        IAnalyticsPdfService analyticsPdfService,
        ISettingsService settingsService)
    {
        _analyticsService = analyticsService;
        _analyticsPdfService = analyticsPdfService;
        _settingsService = settingsService;
    }

    /// <summary>Loads the settings and every analytical report of the current range.</summary>
    public async Task LoadAsync()
    {
        Settings = await _settingsService.GetSettingsAsync();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            _bundle = await _analyticsService.BuildAsync(StartDate, EndDate);
            PeriodLabel = _bundle.PeriodLabel;
            Kpis = new ObservableCollection<AnalyticsKpi>(_bundle.Kpis);
            Sections = new ObservableCollection<AnalyticsReportView>(
                _bundle.Reports.Select(AnalyticsReportView.From));
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ في تحميل التقارير";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyPeriodAsync(string period)
    {
        var today = DateTime.Today;
        switch (period)
        {
            case "daily":
                StartDate = today;
                EndDate = today;
                PeriodLabel = "يومي";
                break;
            case "weekly":
                StartDate = today.AddDays(-6);
                EndDate = today;
                PeriodLabel = "أسبوعي";
                break;
            case "monthly":
                StartDate = new DateTime(today.Year, today.Month, 1);
                EndDate = today;
                PeriodLabel = "شهري";
                break;
            default:
                StartDate = null;
                EndDate = null;
                PeriodLabel = "كل الفترات";
                break;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    [RelayCommand]
    private async Task ExportReportAsync(AnalyticsReportKind kind)
    {
        if (_bundle == null)
            return;

        try
        {
            var filePath = await _analyticsPdfService.GenerateAnalyticsReportAsync(_bundle, kind, Settings);
            StatusMessage = $"تم حفظ التقرير: {filePath}";
            OpenFile(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ في تصدير التقرير";
        }
    }

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        if (_bundle == null)
            return;

        try
        {
            var filePath = await _analyticsPdfService.GenerateFullAnalyticsReportAsync(_bundle, Settings);
            StatusMessage = $"تم حفظ التقرير الشامل: {filePath}";
            OpenFile(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(ex.ToString());
            StatusMessage = $"خطأ في تصدير التقرير الشامل";
        }
    }

    private static void OpenFile(string filePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }
}

/// <summary>One report as the view renders it: the four column headers, the rows
/// with a displayable position, and the empty-state message.</summary>
public sealed class AnalyticsReportView
{
    private AnalyticsReportView(AnalyticsReport report, IReadOnlyList<AnalyticsRowView> rows)
    {
        Kind = report.Kind;
        Title = report.Title;
        Columns = report.Columns;
        Rows = rows;
        EmptyMessage = report.EmptyMessage;
    }

    public AnalyticsReportKind Kind { get; }

    public string Title { get; }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<AnalyticsRowView> Rows { get; }

    public string EmptyMessage { get; }

    public bool IsEmpty => Rows.Count == 0;

    public bool HasRows => Rows.Count > 0;

    public string Column0 => Column(0);
    public string Column1 => Column(1);
    public string Column2 => Column(2);
    public string Column3 => Column(3);

    private string Column(int index) => Columns.Count > index ? Columns[index] : string.Empty;

    /// <summary>Projects a report for display, numbering the rows that the service
    /// did not rank so every table has a position column.</summary>
    public static AnalyticsReportView From(AnalyticsReport report)
    {
        var rows = new List<AnalyticsRowView>(report.Rows.Count);
        var index = 0;
        foreach (var row in report.Rows)
        {
            index++;
            rows.Add(new AnalyticsRowView
            {
                Rank = string.IsNullOrEmpty(row.Rank) ? index.ToString() : row.Rank,
                Primary = row.Primary,
                Secondary = row.Secondary,
                Tertiary = row.Tertiary,
                Value = row.Value,
                BarRatio = row.BarRatio
            });
        }

        return new AnalyticsReportView(report, rows);
    }
}

/// <summary>One rendered row of an analytical report.</summary>
public sealed class AnalyticsRowView
{
    public string Rank { get; init; } = string.Empty;

    public string Primary { get; init; } = string.Empty;

    public string Secondary { get; init; } = string.Empty;

    public string Tertiary { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public double BarRatio { get; init; }
}
