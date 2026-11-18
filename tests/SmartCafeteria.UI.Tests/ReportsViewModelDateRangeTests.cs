using Moq;
using System.IO;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;
using SmartCafeteria.ViewModels;
using Xunit;

namespace SmartCafeteria.UI.Tests;

/// <summary>Covers the date range the reports screen hands to the analytical reports
/// and to the general financial report. These tests never start the app and never
/// touch the database: every service is a mock.</summary>
public class ReportsViewModelDateRangeTests
{
    private static readonly DateTime MarchStart = new(2026, 3, 1);
    private static readonly DateTime MarchEnd = new(2026, 3, 31);

    /// <summary>A path that does not exist. The export command tries to open the file
    /// it was handed, and the failure is swallowed by the view model.</summary>
    private static string MissingFile => Path.Combine(Path.GetTempPath(), "smart-cafeteria-missing-report.pdf");

    private sealed class Harness
    {
        public ReportsViewModel ViewModel { get; set; } = null!;
        public Mock<IAnalyticsService> Analytics { get; } = new();
        public Mock<IAnalyticsPdfService> Pdf { get; } = new();
        public AnalyticsBundle? ExportedBundle { get; set; }
        public FinancialReport? ExportedFinancial { get; set; }
    }

    private static string RangeLabel(DateTime? start, DateTime? end) =>
        $"{start?.ToString("yyyy-MM-dd") ?? "all"}..{end?.ToString("yyyy-MM-dd") ?? "all"}";

    private static Harness CreateHarness()
    {
        var harness = new Harness();

        harness.Analytics
            .Setup(a => a.BuildAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync((DateTime? start, DateTime? end) => new AnalyticsBundle
            {
                Start = start,
                End = end,
                PeriodLabel = RangeLabel(start, end),
                Summary = new AnalyticsSummary(),
                Kpis = Array.Empty<AnalyticsKpi>(),
                Reports = new[]
                {
                    new AnalyticsReport
                    {
                        Kind = AnalyticsReportKind.TopItems,
                        Title = "الأكثر مبيعاً",
                        Columns = Array.Empty<string>(),
                        Rows = Array.Empty<AnalyticsRow>()
                    }
                },
                Financial = new FinancialReport
                {
                    PeriodLabel = RangeLabel(start, end),
                    Currency = "ر.س",
                    Operations = Array.Empty<FinancialOperation>(),
                    Summary = new FinancialSummary()
                }
            });

        harness.Pdf
            .Setup(p => p.GenerateAnalyticsReportAsync(It.IsAny<AnalyticsBundle>(), It.IsAny<AnalyticsReportKind>(), It.IsAny<AppSettings>()))
            .ReturnsAsync((AnalyticsBundle bundle, AnalyticsReportKind kind, AppSettings settings) =>
            {
                harness.ExportedBundle = bundle;
                return MissingFile;
            });

        harness.Pdf
            .Setup(p => p.GenerateFinancialReportAsync(It.IsAny<FinancialReport>(), It.IsAny<AppSettings>()))
            .ReturnsAsync((FinancialReport report, AppSettings settings) =>
            {
                harness.ExportedFinancial = report;
                return MissingFile;
            });

        var orders = new Mock<IOrderService>();
        orders.Setup(o => o.GetAllOrdersAsync()).ReturnsAsync(Array.Empty<Order>());
        var products = new Mock<IProductService>();
        products.Setup(p => p.GetAllProductsAsync()).ReturnsAsync(Array.Empty<Product>());
        var logs = new Mock<ILogService>();
        logs.Setup(l => l.GetAllLogsAsync()).ReturnsAsync(Array.Empty<SystemLog>());
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.GetSettingsAsync()).ReturnsAsync(new AppSettings());

        harness.ViewModel = new ReportsViewModel(
            orders.Object,
            products.Object,
            settings.Object,
            new Mock<IPdfService>().Object,
            logs.Object,
            harness.Analytics.Object,
            harness.Pdf.Object);

        return harness;
    }

    [Fact]
    public async Task AnalyticalReport_UsesTheRangeTheDateFieldsHold()
    {
        var harness = CreateHarness();

        // The screen loads when the reports tab is opened, with the default range.
        await harness.ViewModel.LoadStatsAsync();

        // The user then picks a custom range in the date fields.
        harness.ViewModel.StartDate = MarchStart;
        harness.ViewModel.EndDate = MarchEnd;

        await harness.ViewModel.DownloadAnalyticsReportCommand.ExecuteAsync(AnalyticsReportKind.TopItems);

        Assert.Equal(RangeLabel(MarchStart, MarchEnd), harness.ExportedBundle?.PeriodLabel);
        harness.Analytics.Verify(a => a.BuildAsync(MarchStart, MarchEnd), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GeneralFinancialReport_UsesTheRangeTheDateFieldsHold()
    {
        var harness = CreateHarness();

        await harness.ViewModel.LoadStatsAsync();

        harness.ViewModel.StartDate = MarchStart;
        harness.ViewModel.EndDate = MarchEnd;

        await harness.ViewModel.DownloadFinancialReportCommand.ExecuteAsync(null);

        Assert.Equal(RangeLabel(MarchStart, MarchEnd), harness.ExportedFinancial?.PeriodLabel);
        harness.Analytics.Verify(a => a.BuildAsync(MarchStart, MarchEnd), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChangingTheDateFields_ReloadsTheScreenForTheNewRange()
    {
        var harness = CreateHarness();

        await harness.ViewModel.LoadStatsAsync();

        harness.ViewModel.StartDate = MarchStart;
        harness.ViewModel.EndDate = MarchEnd;
        await harness.ViewModel.PendingReload;

        harness.Analytics.Verify(a => a.BuildAsync(MarchStart, MarchEnd), Times.AtLeastOnce);
        Assert.Equal("مخصص", harness.ViewModel.SelectedPeriodLabel);
    }

    [Fact]
    public async Task PresetPeriod_KeepsItsOwnRangeAndLabel()
    {
        var harness = CreateHarness();

        await harness.ViewModel.LoadStatsAsync();

        await harness.ViewModel.ApplyPeriodCommand.ExecuteAsync("daily");

        Assert.Equal("يومي", harness.ViewModel.SelectedPeriodLabel);
        Assert.Equal(DateTime.Today, harness.ViewModel.StartDate);
        harness.Analytics.Verify(a => a.BuildAsync(DateTime.Today, DateTime.Today), Times.AtLeastOnce);
    }
}
