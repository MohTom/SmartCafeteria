using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Services;

public class PdfReportService : IPdfService
{
    private const string BrandGreen = "#1E7A46";
    private const string LightGreen = "#EAF4EE";
    private const string LightGrey = "#F4F7F5";
    private const string DarkText = "#1A1A1A";

    private static readonly object FontLock = new();
    private static bool _arabicFontReady;

    /// <summary>Registers the embedded Cairo font so Arabic text renders in PDFs.
    /// QuestPDF's default fonts contain no Arabic glyphs (cells would print blank).
    /// Safe to call when the font is missing: reports fall back to the default font.</summary>
    private static void EnsureArabicFont()
    {
        if (_arabicFontReady) return;
        lock (FontLock)
        {
            if (_arabicFontReady) return;
            try
            {
                using var stream = typeof(PdfReportService).Assembly
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

    public Task<string> GenerateSalesReportAsync(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate)
        => Task.Run(() => GenerateSalesReport(orders, settings, startDate, endDate));

    public Task<string> GenerateOrdersReportAsync(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate)
        => Task.Run(() => GenerateOrdersReport(orders, settings, startDate, endDate));

    public Task<string> GenerateInventoryReportAsync(IEnumerable<Product> products, AppSettings settings)
        => Task.Run(() => GenerateInventoryReport(products, settings));

    public Task<string> GenerateOperationsLogReportAsync(IEnumerable<SystemLog> logs, AppSettings settings, DateTime? startDate, DateTime? endDate)
        => Task.Run(() => GenerateOperationsLogReport(logs, settings, startDate, endDate));

    /// <summary>Deletes generated *Report_*.pdf files older than 7 days from the temp folder.</summary>
    private static void CleanupOldReports()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "*Report_*.pdf"))
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
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("Report cleanup failed: {0}", ex);
        }
    }

    /// <summary>Normalizes an optional date range to whole days. The end date is
    /// inclusive: without this, an end date of "today" (midnight) silently
    /// excluded every order placed today and exports came out empty.</summary>
    private static (DateTime? Start, DateTime? End) NormalizeRange(DateTime? startDate, DateTime? endDate)
    {
        DateTime? start = startDate?.Date;
        DateTime? end = endDate?.Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }

    private static string FormatPeriod(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
            return $"من {startDate:yyyy/MM/dd} إلى {endDate:yyyy/MM/dd}";
        if (startDate.HasValue)
            return $"من {startDate:yyyy/MM/dd}";
        if (endDate.HasValue)
            return $"حتى {endDate:yyyy/MM/dd}";
        return "كل الفترات";
    }

    private static void ApplyPageStyle(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(x =>
        {
            var style = x.FontSize(12).FontColor(DarkText);
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

    private static void TableHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell().Background(BrandGreen).Padding(6)
            .Text(text).FontColor(Colors.White).Bold().FontSize(11).AlignRight();
    }

    private static IContainer TableRowCell(TableDescriptor table, bool shaded)
    {
        // Single fluent chain: configuring the same cell in two statements
        // attaches two children and QuestPDF rejects it at compose time.
        var cell = table.Cell().Padding(6);
        return shaded ? cell.Background(LightGrey) : cell;
    }

    public string GenerateSalesReport(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate)
    {
        EnsureArabicFont();
        var (start, end) = NormalizeRange(startDate, endDate);
        var completedOrders = orders.Where(o => o.Status == OrderStatus.Received).ToList();
        if (start.HasValue) completedOrders = completedOrders.Where(o => o.Timestamp >= start.Value).ToList();
        if (end.HasValue) completedOrders = completedOrders.Where(o => o.Timestamp <= end.Value).ToList();

        var totalRevenue = completedOrders.Sum(o => o.Total);
        var totalOrders = completedOrders.Count;
        var averageOrder = totalOrders > 0 ? totalRevenue / totalOrders : 0;

        CleanupOldReports();
        var filePath = Path.Combine(Path.GetTempPath(), $"SalesReport_{Guid.NewGuid():N}.pdf");

        QuestPdfConfiguration.EnsureCommunityLicense();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "تقرير المبيعات", FormatPeriod(startDate, endDate)));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "إجمالي الإيرادات", $"{totalRevenue:F2} {settings.CurrencyAr}");
                        SummaryCard(row.RelativeItem(), "عدد الطلبات", $"{totalOrders}");
                        SummaryCard(row.RelativeItem(), "متوسط الطلب", $"{averageOrder:F2} {settings.CurrencyAr}");
                    });

                    col.Item().PaddingTop(16);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            TableHeaderCell(header, "الرقم");
                            TableHeaderCell(header, "الكود");
                            TableHeaderCell(header, "التاريخ");
                            TableHeaderCell(header, "طريقة الدفع");
                            TableHeaderCell(header, "نوع الطلب");
                            TableHeaderCell(header, "المبلغ");
                        });

                        var shaded = false;
                        foreach (var order in completedOrders)
                        {
                            TableRowCell(table, shaded).Text(order.Id.Length > 10 ? order.Id[^10..] : order.Id).FontSize(10).AlignRight();
                            TableRowCell(table, shaded).Text(order.Code).AlignRight();
                            TableRowCell(table, shaded).Text(order.Timestamp.ToString("yyyy/MM/dd HH:mm")).AlignRight();
                            TableRowCell(table, shaded).Text(GetPaymentMethodArabic(order.PaymentMethod)).AlignRight();
                            TableRowCell(table, shaded).Text(order.OrderType.ToArabic()).AlignRight();
                            TableRowCell(table, shaded).Text($"{order.Total:F2}").AlignRight();
                            shaded = !shaded;
                        }

                        if (completedOrders.Count == 0)
                            table.Cell().ColumnSpan(6).Padding(10)
                                .Text("لا توجد بيانات في النطاق المحدد").Italic().AlignCenter();
                    });
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
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

    public string GenerateOrdersReport(IEnumerable<Order> orders, AppSettings settings, DateTime? startDate, DateTime? endDate)
    {
        EnsureArabicFont();
        var (start, end) = NormalizeRange(startDate, endDate);
        var filteredOrders = orders.ToList();
        if (start.HasValue) filteredOrders = filteredOrders.Where(o => o.Timestamp >= start.Value).ToList();
        if (end.HasValue) filteredOrders = filteredOrders.Where(o => o.Timestamp <= end.Value).ToList();

        CleanupOldReports();
        var filePath = Path.Combine(Path.GetTempPath(), $"OrdersReport_{Guid.NewGuid():N}.pdf");

        QuestPdfConfiguration.EnsureCommunityLicense();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "تقرير الطلبات", FormatPeriod(startDate, endDate)));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "عدد الطلبات", $"{filteredOrders.Count}");
                        SummaryCard(row.RelativeItem(), "الإجمالي", $"{filteredOrders.Sum(o => o.Total):F2} {settings.CurrencyAr}");
                    });

                    col.Item().PaddingTop(16);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            TableHeaderCell(header, "الكود");
                            TableHeaderCell(header, "التاريخ");
                            TableHeaderCell(header, "الحالة");
                            TableHeaderCell(header, "طريقة الدفع");
                            TableHeaderCell(header, "نوع الطلب");
                            TableHeaderCell(header, "ملاحظات");
                            TableHeaderCell(header, "الأصناف");
                            TableHeaderCell(header, "المبلغ");
                        });

                        var shaded = false;
                        foreach (var order in filteredOrders)
                        {
                            TableRowCell(table, shaded).Text(order.Code).AlignRight();
                            TableRowCell(table, shaded).Text(order.Timestamp.ToString("yyyy/MM/dd HH:mm")).AlignRight();
                            TableRowCell(table, shaded).Text(GetStatusArabic(order.Status)).AlignRight();
                            TableRowCell(table, shaded).Text(GetPaymentMethodArabic(order.PaymentMethod)).AlignRight();
                            TableRowCell(table, shaded).Text(order.OrderType.ToArabic()).AlignRight();
                            TableRowCell(table, shaded).Text(order.Notes ?? string.Empty).AlignRight();
                            TableRowCell(table, shaded).Text($"{order.Items.Count}").AlignRight();
                            TableRowCell(table, shaded).Text($"{order.Total:F2}").AlignRight();
                            shaded = !shaded;
                        }

                        if (filteredOrders.Count == 0)
                            table.Cell().ColumnSpan(8).Padding(10)
                                .Text("لا توجد بيانات في النطاق المحدد").Italic().AlignCenter();
                    });
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    }

    public string GenerateInventoryReport(IEnumerable<Product> products, AppSettings settings)
    {
        EnsureArabicFont();
        var productList = products.ToList();
        var lowStockThreshold = settings.LowStockThreshold;
        CleanupOldReports();
        var filePath = Path.Combine(Path.GetTempPath(), $"InventoryReport_{Guid.NewGuid():N}.pdf");

        QuestPdfConfiguration.EnsureCommunityLicense();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "تقرير المخزون", FormatPeriod(null, null)));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "عدد المنتجات", $"{productList.Count}");
                        SummaryCard(row.RelativeItem(), "مخزون منخفض", $"{productList.Count(p => p.Inventory <= lowStockThreshold)}");
                        SummaryCard(row.RelativeItem(), "نافد", $"{productList.Count(p => p.Inventory == 0)}");
                    });

                    col.Item().PaddingTop(16);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            TableHeaderCell(header, "المنتج");
                            TableHeaderCell(header, "الفئة");
                            TableHeaderCell(header, "المخزون الحالي");
                            TableHeaderCell(header, "الحد الأقصى");
                            TableHeaderCell(header, "الحالة");
                        });

                        var shaded = false;
                        foreach (var product in productList)
                        {
                            var status = product.Inventory == 0 ? "نفد" :
                                        product.Inventory <= lowStockThreshold ? "منخفض" : "متوفر";

                            TableRowCell(table, shaded).Text(product.NameAr).AlignRight();
                            TableRowCell(table, shaded).Text(GetCategoryNameArabic(product.Category)).AlignRight();
                            TableRowCell(table, shaded).Text($"{product.Inventory}").AlignRight();
                            TableRowCell(table, shaded).Text($"{product.MaxInventory}").AlignRight();
                            TableRowCell(table, shaded).Text(status).AlignRight();
                            shaded = !shaded;
                        }

                        if (productList.Count == 0)
                            table.Cell().ColumnSpan(5).Padding(10)
                                .Text("لا توجد بيانات").Italic().AlignCenter();
                    });
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    }

    public string GenerateOperationsLogReport(IEnumerable<SystemLog> logs, AppSettings settings, DateTime? startDate, DateTime? endDate)
    {
        EnsureArabicFont();
        var (start, end) = NormalizeRange(startDate, endDate);
        var filteredLogs = logs.ToList();
        if (start.HasValue) filteredLogs = filteredLogs.Where(l => l.Timestamp >= start.Value).ToList();
        if (end.HasValue) filteredLogs = filteredLogs.Where(l => l.Timestamp <= end.Value).ToList();
        filteredLogs = filteredLogs.OrderByDescending(l => l.Timestamp).ToList();

        CleanupOldReports();
        var filePath = Path.Combine(Path.GetTempPath(), $"OperationsLogReport_{Guid.NewGuid():N}.pdf");

        QuestPdfConfiguration.EnsureCommunityLicense();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ApplyPageStyle(page);
                page.Header().Column(header => ReportHeader(header, settings, "تقرير سجل العمليات", FormatPeriod(startDate, endDate)));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "عدد العمليات", $"{filteredLogs.Count}");
                    });

                    col.Item().PaddingTop(16);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            TableHeaderCell(header, "الوقت");
                            TableHeaderCell(header, "المستوى");
                            TableHeaderCell(header, "العملية");
                        });

                        var shaded = false;
                        foreach (var log in filteredLogs)
                        {
                            TableRowCell(table, shaded).Text(log.Timestamp.ToString("yyyy/MM/dd HH:mm")).AlignRight();
                            TableRowCell(table, shaded).Text(GetLogLevelArabic(log.Level)).AlignRight();
                            TableRowCell(table, shaded).Text(log.Message).AlignRight();
                            shaded = !shaded;
                        }

                        if (filteredLogs.Count == 0)
                            table.Cell().ColumnSpan(3).Padding(10)
                                .Text("لا توجد بيانات في النطاق المحدد").Italic().AlignCenter();
                    });
                });

                ReportFooter(page, settings);
            });
        }).GeneratePdf(filePath);

        return filePath;
    }

    private static string GetPaymentMethodArabic(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Bankak => "بنكك",
            _ => method.ToString()
        };
    }

    private static string GetStatusArabic(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Received => "تم الاستلام",
            OrderStatus.Cancelled => "ملغي",
            _ => status.ToString()
        };
    }

    private static string GetLogLevelArabic(string level)
    {
        return level switch
        {
            "success" => "نجاح",
            "info" => "معلومة",
            "warning" => "تحذير",
            "error" => "خطأ",
            "alert" => "تنبيه",
            _ => level
        };
    }

    private static string GetCategoryNameArabic(string category)
    {
        return category switch
        {
            "HotDrinks" => "مشروبات ساخنة",
            "ColdDrinks" => "مشروبات باردة",
            "Breakfast" => "إفطار",
            "Sandwiches" => "ساندويتشات",
            "SweetPies" => "حلويات",
            "SavoryPies" => "فطائر مالحة",
            "Desserts" => "حلوى",
            "Juices" => "عصائر",
            "Grills" => "مشويات",
            _ => category
        };
    }
}

