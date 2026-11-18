using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Models.Analytics;

namespace SmartCafeteria.Services;

public sealed class AnalyticsPdfService : IAnalyticsPdfService
{
    private const string BrandGreen = "#1E7A46";
    private const string LightGreen = "#EAF4EE";
    private const string LightGrey = "#F4F7F5";
    private const string DarkText = "#1A1A1A";

    /// <summary>Width of an A4 page, in points.</summary>
    public const float PageWidth = 595.276f;

    /// <summary>Label of the first column of an analytical table.</summary>
    public const string RankHeader = "الترتيب";

    /// <summary>Margin on every side of the page, in points.</summary>
    public const float PageMargin = 40;

    /// <summary>Padding inside every table cell, in points.</summary>
    public const float CellPadding = 6;

    /// <summary>Width of the rank column, in points. The column keeps a fixed width so
    /// that its header stays on one line.</summary>
    public const float RankColumnWidth = 50;

    /// <summary>Weights of the four data columns that follow the rank column. They
    /// share the width the rank column leaves behind.</summary>
    private static readonly float[] DataColumnWeights = { 2.5f, 2.05f, 2.9f, 2.15f };

    /// <summary>Widths in points of the columns of an analytical table, in display order.
    /// The rank column keeps a fixed width and the data columns share the rest of the
    /// page.</summary>
    public static IReadOnlyList<float> AnalyticalColumnWidths()
    {
        var unit = (PageWidth - 2 * PageMargin - RankColumnWidth) / DataColumnWeights.Sum();

        var widths = new List<float> { RankColumnWidth };
        foreach (var weight in DataColumnWeights)
            widths.Add(weight * unit);

        return widths;
    }

    private static readonly object FontLock = new();
    private static bool _arabicFontReady;

    private static void EnsureArabicFont()
    {
        if (_arabicFontReady) return;
        lock (FontLock)
        {
            if (_arabicFontReady) return;
            try
            {
                using var stream = typeof(AnalyticsPdfService).Assembly
                    .GetManifestResourceStream("SmartCafeteria.Services.Fonts.Cairo.ttf");
                if (stream != null)
                {
                    FontManager.RegisterFont(stream);
                    _arabicFontReady = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Arabic font registration failed; falling back to the default font: {0}", ex);
            }
        }
    }

    public Task<string> GenerateAnalyticsReportAsync(AnalyticsBundle bundle, AnalyticsReportKind kind, AppSettings settings)
        => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(settings);

        var report = bundle.Find(kind);
        if (report == null)
            throw new ArgumentException($"غير موجود", nameof(kind));

        EnsureArabicFont();
        CleanupOldReports();
        QuestPdfConfiguration.EnsureCommunityLicense();

        var filePath = Path.Combine(Path.GetTempPath(), $"AnalyticsReport_{kind}_{Guid.NewGuid():N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, report.Title, bundle.PeriodLabel));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    ReportTable(col.Item(), report);
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    });

    public Task<string> GenerateFullAnalyticsReportAsync(AnalyticsBundle bundle, AppSettings settings)
        => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(settings);

        EnsureArabicFont();
        CleanupOldReports();
        QuestPdfConfiguration.EnsureCommunityLicense();

        var filePath = Path.Combine(Path.GetTempPath(), $"AnalyticsFullReport_{Guid.NewGuid():N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "التقرير التحليلي الشامل", bundle.PeriodLabel));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    KpiCards(col, bundle.Kpis);

                    foreach (var report in bundle.Reports)
                    {
                        col.Item().PaddingTop(14);
                        col.Item().Text(report.Title).Bold().FontSize(13).FontColor(BrandGreen);
                        col.Item().PaddingTop(6);
                        ReportTable(col.Item(), report);
                    }
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    });

    public Task<string> GenerateFinancialReportAsync(FinancialReport report, AppSettings settings)
        => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(settings);

        EnsureArabicFont();
        CleanupOldReports();
        QuestPdfConfiguration.EnsureCommunityLicense();

        var filePath = Path.Combine(Path.GetTempPath(), $"FinancialReport_{Guid.NewGuid():N}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "التقرير المالي الشامل", report.PeriodLabel));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    FinancialLedger(col.Item(), report);
                    col.Item().PaddingTop(14);
                    FinancialSummaryBlock(col.Item(), report);
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    });

    private static void CleanupOldReports()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var pattern in new[] { "AnalyticsReport_*.pdf", "AnalyticsFullReport_*.pdf", "FinancialReport_*.pdf" })
            {
                foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), pattern))
                {
                    try
                    {
                        if (File.GetCreationTime(file) < cutoff)
                            File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "Old report {0} could not be deleted: {1}", file, ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Report cleanup failed: {0}", ex);
        }
    }

    private static void ApplyPageStyle(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(PageMargin);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(x =>
        {
            var style = x.FontSize(10).FontColor(DarkText);
            return _arabicFontReady ? style.FontFamily("Cairo") : style;
        });
    }

    private static void ReportHeader(ColumnDescriptor header, AppSettings settings, string title, string period)
    {
        header.Item().Background(BrandGreen).Padding(12).AlignCenter()
            .Text(settings.CafeteriaNameAr).FontColor(Colors.White).Bold().FontSize(18);
        header.Item().PaddingTop(8).AlignCenter()
            .Text(title).Bold().FontSize(15).FontColor(BrandGreen);
        header.Item().AlignCenter().Text(period).FontSize(10);
        header.Item().AlignCenter().Text($"تاريخ الإصدار: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(9);
        header.Item().PaddingBottom(6);
    }

    private static void ReportFooter(PageDescriptor page, AppSettings settings)
    {
        page.Footer().Row(row =>
        {
            row.RelativeItem().Text(settings.CafeteriaAddress).FontSize(8).AlignCenter();
            row.ConstantItem(120).Text(t =>
            {
                t.AlignLeft();
                t.Span("صفحة ").FontSize(8);
                t.CurrentPageNumber().FontSize(8);
                t.Span(" من ").FontSize(8);
                t.TotalPages().FontSize(8);
            });
        });
    }

    private static void SummaryCard(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(BrandGreen).Background(LightGreen)
            .Padding(10).AlignCenter().Column(c =>
            {
                c.Item().Text(label).Bold().FontSize(11).FontColor(BrandGreen);
                c.Item().PaddingTop(4).Text(value).Bold().FontSize(14);
            });
    }

    private static void FinancialLedger(IContainer container, FinancialReport report)
    {
        container.Column(col =>
        {
            col.Item().Text("سجل العمليات المالية").Bold().FontSize(13).FontColor(BrandGreen);
            col.Item().PaddingTop(6);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(65);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(2.0f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.9f);
                });

                table.Header(header =>
                {
                    TableHeaderCell(header, "التاريخ");
                    TableHeaderCell(header, "النوع");
                    TableHeaderCell(header, "البيان");
                    TableHeaderCell(header, "الجهة");
                    TableHeaderCell(header, "الدفع");
                    TableHeaderCell(header, "الاتجاه");
                    TableHeaderCell(header, "المبلغ");
                });

                var shaded = false;
                foreach (var operation in report.Operations)
                {
                    TableRowCell(table, shaded).Text(operation.Date.ToString("yyyy/MM/dd")).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(operation.KindLabel).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(operation.Description).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(operation.Party).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(operation.PaymentLabel).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(DirectionLabel(operation.Direction)).FontSize(8).AlignRight();
                    TableRowCell(table, shaded).Text(AmountText(operation, report.Currency)).FontSize(8).AlignRight();
                    shaded = !shaded;
                }

                if (report.Operations.Count == 0)
                    table.Cell().ColumnSpan(7).Padding(10).Text("لا توجد عمليات مالية في النطاق المحدد").Italic().AlignCenter();
            });
        });
    }

    private static void FinancialSummaryBlock(IContainer container, FinancialReport report)
    {
        container.Column(col =>
        {
            col.Item().Text("الملخص المالي").Bold().FontSize(13).FontColor(BrandGreen);
            col.Item().PaddingTop(6);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3f);
                    columns.RelativeColumn(1.4f);
                });

                SummaryRow(table, "إيرادات المبيعات", Money(report.Summary.Sales, report.Currency), false);
                SummaryRow(table, "إيرادات أخرى", Money(report.Summary.OtherIncome, report.Currency), true);
                SummaryRow(table, "إجمالي التدفق الداخل", Money(report.Summary.TotalInflow, report.Currency), false);
                SummaryRow(table, "دفعات الموردين", Money(report.Summary.SupplierPayments, report.Currency), true);
                SummaryRow(table, "رواتب الموظفين", Money(report.Summary.SalaryPayments, report.Currency), false);
                SummaryRow(table, "مصروفات أخرى", Money(report.Summary.OtherExpenses, report.Currency), true);
                SummaryRow(table, "إجمالي التدفق الخارج", Money(report.Summary.TotalOutflow, report.Currency), false);
                SummaryRow(table, "صافي التدفق النقدي", Money(report.Summary.NetCashFlow, report.Currency), true);
                SummaryRow(table, "مشتريات الموردين (التزامات)", Money(report.Summary.SupplierPurchases, report.Currency), false);
                SummaryRow(table, "حركة الموردين (مشتريات - مدفوعات)", Money(report.Summary.SupplierMovement, report.Currency), true);
                SummaryRow(table, "عدد العمليات", report.Summary.Operations.ToString(), false);
            });
        });
    }

    private static void SummaryRow(TableDescriptor table, string label, string value, bool shaded)
    {
        TableRowCell(table, shaded).Text(label).FontSize(9).AlignRight();
        TableRowCell(table, shaded).Text(value).FontSize(9).Bold().AlignRight();
    }

    private static string DirectionLabel(FinancialDirection direction) => direction switch
    {
        FinancialDirection.Inflow => "وارد",
        FinancialDirection.Outflow => "صادر",
        FinancialDirection.Commitment => "التزام",
        _ => direction.ToString()
    };

    private static string AmountText(FinancialOperation operation, string currency)
    {
        var money = Money(operation.Amount, currency);
        return operation.Direction == FinancialDirection.Inflow
            ? "+" + money
            : operation.Direction == FinancialDirection.Commitment
                ? "التزام " + money
                : "-" + money;
    }

    private static string Money(decimal amount, string currency)
        => amount.ToString("N2") + " " + currency;

    private static void KpiCards(ColumnDescriptor column, IReadOnlyList<AnalyticsKpi> kpis)
    {
        if (kpis.Count == 0) return;
        foreach (var chunk in kpis.Chunk(4))
        {
            column.Item().Row(row =>
            {
                foreach (var kpi in chunk)
                    SummaryCard(row.RelativeItem(), kpi.Label, kpi.Value);
            });
            column.Item().PaddingBottom(6);
        }
    }

    private static void ReportTable(IContainer container, AnalyticsReport report)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(RankColumnWidth);
                columns.RelativeColumn(DataColumnWeights[0]);
                columns.RelativeColumn(DataColumnWeights[1]);
                columns.RelativeColumn(DataColumnWeights[2]);
                columns.RelativeColumn(DataColumnWeights[3]);
            });

            table.Header(header =>
            {
                TableHeaderCell(header, RankHeader);
                TableHeaderCell(header, Column(report, 0));
                TableHeaderCell(header, Column(report, 1));
                TableHeaderCell(header, Column(report, 2));
                TableHeaderCell(header, Column(report, 3));
            });

            var shaded = false;
            var index = 0;
            foreach (var row in report.Rows)
            {
                index++;
                var rank = string.IsNullOrEmpty(row.Rank) ? index.ToString() : row.Rank;
                TableRowCell(table, shaded).Text(rank).FontSize(9).AlignRight();
                TableRowCell(table, shaded).Text(row.Primary).FontSize(10).AlignRight();
                TableRowCell(table, shaded).Text(row.Secondary).FontSize(10).AlignRight();
                TableRowCell(table, shaded).Text(row.Tertiary).FontSize(10).AlignRight();
                TableRowCell(table, shaded).Text(row.Value).FontSize(10).AlignRight();
                shaded = !shaded;
            }

            if (report.Rows.Count == 0)
                table.Cell().ColumnSpan(5).Padding(10).Text(report.EmptyMessage).Italic().AlignCenter();
        });
    }

    private static string Column(AnalyticsReport report, int index)
        => report.Columns.Count > index ? report.Columns[index] : string.Empty;

    private static void TableHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background(BrandGreen).Padding(CellPadding)
            .Text(text).FontColor(Colors.White).Bold().FontSize(11).AlignRight();
    }

    private static IContainer TableRowCell(TableDescriptor table, bool shaded)
    {
        var cell = table.Cell().Padding(CellPadding);
        return shaded ? cell.Background(LightGrey) : cell;
    }
}
