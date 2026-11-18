using System.Drawing;
using System.Drawing.Printing;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Printing;

public class ThermalPrinter : IDisposable, IPrintService
{
    private readonly ITicketConfigStore _configStore;

    private PrintDocument? _printDocument;
    private TicketPrintModel? _currentModel;
    private string _printerName = string.Empty;

    public ThermalPrinter() : this(new TicketConfigStore())
    {
    }

    public ThermalPrinter(string printerName) : this(new TicketConfigStore(), printerName)
    {
    }

    public ThermalPrinter(ITicketConfigStore configStore) : this(configStore, string.Empty)
    {
    }

    public ThermalPrinter(ITicketConfigStore configStore, string printerName)
    {
        _configStore = configStore ?? new TicketConfigStore();
        _printerName = printerName ?? string.Empty;
    }

    public string PrinterName
    {
        get => _printerName;
        set => _printerName = value ?? string.Empty;
    }

    public Task PrintReceiptAsync(Order order, AppSettings settings)
        => Task.Run(() =>
        {
            using var printer = new ThermalPrinter(_configStore, ResolvePrinterName(settings));
            printer.PrintTicket(PrintReceiptBuilder.BuildReceipt(order, settings));
        });

    public Task PrintCashierSummaryAsync(Order order, AppSettings settings)
        => Task.Run(() =>
        {
            using var printer = new ThermalPrinter(_configStore, ResolvePrinterName(settings));
            printer.PrintTicket(PrintReceiptBuilder.BuildCashierSummaryReceipt(order, settings));
        });

    public Task PrintKitchenReceiptAsync(Order order, string sectionName, List<OrderItem> sectionItems, AppSettings settings, bool minimal = false)
        => Task.Run(() =>
        {
            using var printer = new ThermalPrinter(_configStore, ResolvePrinterName(settings));
            printer.PrintTicket(PrintReceiptBuilder.BuildReceipt(order, settings, sectionName, sectionItems, minimal));
        });

    public Task<List<string>> GetInstalledPrintersAsync() => Task.Run(GetInstalledPrinters);

    public Task<string> GetDefaultPrinterAsync() => Task.Run(GetDefaultPrinter);

    private string ResolvePrinterName(AppSettings settings)
        => string.IsNullOrEmpty(_printerName) ? settings.PrinterName : _printerName;

    public void PrintTicket(TicketPrintModel model)
    {
        _currentModel = model;
        _printDocument = new PrintDocument();

        if (!IsVirtualPrinter(_printerName))
        {
            _printDocument.PrinterSettings.PrinterName = _printerName;
        }

        _printDocument.DefaultPageSettings.Margins = new Margins(20, 20, 20, 30);
        _printDocument.PrintPage += PrintPageHandler;
        _printDocument.Print();

        SendCutCommand();
    }

    private void PrintPageHandler(object sender, PrintPageEventArgs e)
    {
        if (_currentModel == null || e.Graphics == null) return;

        RenderTicket(e.Graphics, _currentModel, e.MarginBounds);

        e.HasMorePages = false;
    }

    /// <summary>
    /// Draws one ticket onto <paramref name="g"/> inside <paramref name="bounds"/>.
    /// Every font size, row height and advance comes from <see cref="TicketLayout"/>,
    /// which resolves the metrics for the resolution of the target device, so the
    /// layout never clips the text and never shifts to the wrong margin.
    /// </summary>
    public void RenderTicket(Graphics g, TicketPrintModel model, RectangleF bounds)
    {
        var fonts = TicketFontSet.FromConfig(_configStore);
        using var layout = new TicketLayout(g, fonts);

        // "Near" + right-to-left is the only combination that puts the text on the
        // right margin: "Far" + right-to-left anchors it on the left instead.
        using var centered = CreateFormat(StringAlignment.Center, StringFormatFlags.DirectionRightToLeft);
        using var rightAligned = CreateFormat(StringAlignment.Near, StringFormatFlags.DirectionRightToLeft);
        using var priceFormat = CreateFormat(StringAlignment.Far, (StringFormatFlags)0, StringFormatFlags.NoWrap);

        var left = bounds.Left;
        var width = bounds.Width;
        float y = bounds.Top;

        if (model.IsCashierSummary)
        {
            // Cashier copy: cafeteria name, the receive label, the receipt number,
            // the order type, the meals and the total. Nothing else.
            g.DrawString(model.Title, layout.TitleFont, Brushes.Black, new RectangleF(left, y, width, layout.TitleHeight), centered);
            y += layout.TitleAdvance;

            g.DrawString(model.SummaryTitle, layout.SectionFont, Brushes.Black, new RectangleF(left, y, width, layout.SectionHeight), centered);
            y += layout.SectionAdvance;

            g.DrawLine(Pens.Black, left, y, left + width, y);
            y += layout.RuleGap;

            g.DrawString($"الرقم: {model.TicketNumber}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.RuleAdvance;

            if (!string.IsNullOrEmpty(model.OrderType))
            {
                g.DrawString($"نوع الطلب: {model.OrderType}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
                y += layout.DetailAdvance;
            }

            g.DrawLine(Pens.Black, left, y, left + width, y);
            y += layout.RuleGap;

            var summaryFooterReserve = layout.BlockGap + layout.RuleGap + layout.TotalAdvance + layout.LineAdvance;
            y = DrawItemLines(g, layout, model, left, width, y, bounds.Bottom, summaryFooterReserve, rightAligned, priceFormat);

            y += layout.BlockGap;
            g.DrawLine(Pens.Black, left, y, left + width, y);
            y += layout.RuleGap;

            g.DrawString($"الإجمالي: {model.Total:F2} {model.CurrencySymbolAr}", layout.TotalFont, Brushes.Black, new RectangleF(left, y, width, layout.TotalHeight), rightAligned);
            return;
        }

        if (model.IsMinimal)
        {
            // Sections-only ticket: cafeteria name, section (own line),
            // address, receipt number and meals. Nothing else.
            g.DrawString(model.Title, layout.TitleFont, Brushes.Black, new RectangleF(left, y, width, layout.TitleHeight), centered);
            y += layout.TitleAdvance;

            g.DrawString(model.SectionName, layout.SectionFont, Brushes.Black, new RectangleF(left, y, width, layout.SectionHeight), centered);
            y += layout.SectionAdvance;

            g.DrawString(model.Subtitle, layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.RuleAdvance;

            g.DrawLine(Pens.Black, left, y, left + width, y);
            y += layout.RuleGap;

            g.DrawString($"الرقم: {model.TicketNumber}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.RuleAdvance;

            // The kitchen needs to know whether the order is for here or to go.
            if (!string.IsNullOrEmpty(model.OrderType))
            {
                g.DrawString($"نوع الطلب: {model.OrderType}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
                y += layout.DetailAdvance;
            }

            g.DrawLine(Pens.Black, left, y, left + width, y);
            y += layout.RuleGap;

            y = DrawItemLines(g, layout, model, left, width, y, bounds.Bottom, layout.MinimalFooterReserve, rightAligned, priceFormat);

            g.DrawString("*******", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);

            return;
        }

        g.DrawString(model.Title, layout.TitleFont, Brushes.Black, new RectangleF(left, y, width, layout.TitleHeight), centered);
        y += layout.TitleAdvance;

        g.DrawString(model.Subtitle, layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.LineAdvance;

        g.DrawString(model.Phone, layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.RuleAdvance;

        g.DrawLine(Pens.Black, left, y, left + width, y);
        y += layout.RuleGap;

        // The receipt number and the date are printed on two lines of their own,
        // so each of them can be read without a second look.
        g.DrawString($"الرقم: {model.TicketNumber}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.DetailAdvance;

        g.DrawString($"التاريخ: {model.Date}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.DetailAdvance;

        if (!string.IsNullOrEmpty(model.CustomerName))
        {
            g.DrawString($"العميل: {model.CustomerName}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.LineAdvance;
        }

        if (!string.IsNullOrEmpty(model.CustomerPhone))
        {
            g.DrawString($"الهاتف: {model.CustomerPhone}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.LineAdvance;
        }

        g.DrawString($"طريقة الدفع: {model.PaymentMethod}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.LineAdvance;

        if (!string.IsNullOrEmpty(model.OrderType))
        {
            g.DrawString($"نوع الطلب: {model.OrderType}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.LineAdvance;
        }

        if (!string.IsNullOrEmpty(model.PickupTime))
        {
            g.DrawString($"وقت الاستلام: {model.PickupTime}", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
            y += layout.LineAdvance;
        }

        y += layout.BlockGap;
        g.DrawLine(Pens.Black, left, y, left + width, y);
        y += layout.RuleGap;

        y = DrawItemLines(g, layout, model, left, width, y, bounds.Bottom, layout.FullFooterReserve, rightAligned, priceFormat);

        y += layout.BlockGap;
        g.DrawLine(Pens.Black, left, y, left + width, y);
        y += layout.RuleGap;

        g.DrawString($"الإجمالي: {model.Total:F2} {model.CurrencySymbolAr}", layout.TotalFont, Brushes.Black, new RectangleF(left, y, width, layout.TotalHeight), rightAligned);
        y += layout.TotalAdvance;

        g.DrawString("شكراً لزيارتكم!", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.DetailAdvance;

        g.DrawString(model.OrderId, layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
        y += layout.LineAdvance;

        g.DrawString("*******", layout.SmallFont, Brushes.Black, new RectangleF(left, y, width, layout.LineHeight), centered);
    }

    /// <summary>
    /// Draws the ordered items: the name of the item on the right margin, the
    /// price in its own column on the left margin. Returns the new cursor.
    /// </summary>
    private static float DrawItemLines(
        Graphics g,
        TicketLayout layout,
        TicketPrintModel model,
        float left,
        float width,
        float y,
        float bottom,
        float footerReserve,
        StringFormat rightAligned,
        StringFormat priceFormat)
    {
        var priceColumnWidth = MeasurePriceColumn(g, layout, model, priceFormat);
        var truncated = false;

        foreach (var line in model.Lines)
        {
            // Reserve room for the footer so the last line never lands half-cut
            // at the paper edge.
            if (y > bottom - footerReserve)
            {
                truncated = true;
                break;
            }

            var font = layout.FontFor(line);
            var price = line.PriceText;

            if (string.IsNullOrEmpty(price) || priceColumnWidth <= 0f || width <= priceColumnWidth)
            {
                DrawRightAligned(g, line.Text, font, new RectangleF(left, y, width, layout.LineHeight), rightAligned);
            }
            else
            {
                g.DrawString(price, font, Brushes.Black, new RectangleF(left, y, priceColumnWidth - layout.PriceGap, layout.LineHeight), priceFormat);

                var nameRect = new RectangleF(left + priceColumnWidth, y, width - priceColumnWidth, layout.LineHeight);
                DrawRightAligned(g, line.Text, font, nameRect, rightAligned);
            }

            y += layout.LineAdvance;
        }

        if (truncated)
        {
            DrawRightAligned(g, "… (تابع)", layout.SmallFont, new RectangleF(left, y, width, layout.LineHeight), rightAligned);
            y += layout.LineAdvance;
        }

        return y;
    }

    /// <summary>Width of the price column: the widest price plus the gap to the name.</summary>
    private static float MeasurePriceColumn(Graphics g, TicketLayout layout, TicketPrintModel model, StringFormat priceFormat)
    {
        var widest = 0f;
        foreach (var line in model.Lines)
        {
            if (string.IsNullOrEmpty(line.PriceText)) continue;

            var font = layout.FontFor(line);
            widest = Math.Max(widest, g.MeasureString(line.PriceText, font, TicketLayout.MeasurementArea, priceFormat).Width);
        }

        return widest <= 0f ? 0f : widest + layout.PriceGap;
    }

    /// <summary>
    /// Draws a text on the right margin of <paramref name="rect"/>. A text that is
    /// too long for the row is shortened instead of wrapping onto a second line,
    /// which the row box would clip.
    /// </summary>
    private static void DrawRightAligned(Graphics g, string text, Font font, RectangleF rect, StringFormat rightAligned)
    {
        if (g.MeasureString(text, font, TicketLayout.MeasurementArea, rightAligned).Width <= rect.Width)
        {
            g.DrawString(text, font, Brushes.Black, rect, rightAligned);
            return;
        }

        using var trimmed = new StringFormat
        {
            Alignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };

        g.DrawString(text, font, Brushes.Black, rect, trimmed);
    }

    private static StringFormat CreateFormat(StringAlignment alignment, StringFormatFlags flags, StringFormatFlags extra = 0)
    {
        return new StringFormat
        {
            Alignment = alignment,
            FormatFlags = flags | extra
        };
    }

    private void SendCutCommand()
    {
        if (IsVirtualPrinter(_printerName)) return;
        try
        {
            // Plain partial cut (GS V NUL) with no paper feed: the previous
            // feed-and-cut command (GS V B) pushed out a small blank slip
            // between receipts.
            byte[] cutCommand = new byte[] { 0x1D, 0x56, 0x00 };
            RawPrinterHelper.SendBytes(_printerName, cutCommand);
        }
        catch (Exception ex)
        {
            // The receipt itself has already been printed; a failed cut must not
            // be reported as a failed sale, but it is never silent.
            System.Diagnostics.Trace.TraceWarning(
                "Printer cut command failed for '{0}': {1}", _printerName, ex.Message);
        }
    }

    private static bool IsVirtualPrinter(string printerName)
    {
        if (string.IsNullOrEmpty(printerName)) return true;
        var name = printerName.ToLowerInvariant();
        return name.Contains("pdf") || name.Contains("xps") || name.Contains("onenote") || name.Contains("fax");
    }

    public static List<string> GetInstalledPrinters()
    {
        var printers = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printer);
        }
        return printers;
    }

    public static string GetDefaultPrinter()
    {
        var settings = new PrinterSettings();
        return settings.PrinterName;
    }

    public void Dispose()
    {
        _printDocument?.Dispose();
    }
}
