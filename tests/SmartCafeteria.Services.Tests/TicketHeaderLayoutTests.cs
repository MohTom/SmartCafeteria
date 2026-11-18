using System.Drawing;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Guards the header of the customer receipt: the receipt number and the date
/// are printed on a line of their own, so each of them can be read without a
/// second look.
/// </summary>
public class TicketHeaderLayoutTests
{
    /// <summary>Smallest share of the line box that still keeps every line apart.</summary>
    private const int SafeLineSpacing = 85;

    /// <summary>
    /// The receipt number and the date must not share a line: the number is
    /// printed above the date.
    /// </summary>
    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void TheReceiptNumber_AndTheDate_ArePrintedOnSeparateLines(int dpi)
    {
        using var baseImage = Render(BuildTicket("1042", "2026-10-21 12:30"), dpi);
        using var numberImage = Render(BuildTicket("1043", "2026-10-21 12:30"), dpi);
        using var dateImage = Render(BuildTicket("1042", "2026-10-21 12:31"), dpi);

        var number = TicketInk.DiffBounds(baseImage, numberImage);
        var date = TicketInk.DiffBounds(baseImage, dateImage);

        Assert.NotNull(number);
        Assert.NotNull(date);

        Assert.True(number!.Value.Bottom < date!.Value.Top,
            $"The receipt number (row {number.Value.Bottom}) and the date (row {date.Value.Top}) are printed on the same line at {dpi} dpi.");
    }

    /// <summary>
    /// The date must still be printed after the split, on a line of its own
    /// below the receipt number.
    /// </summary>
    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void TheDate_IsStillPrintedOnTheReceipt(int dpi)
    {
        using var withDate = Render(BuildTicket("1042", "2026-10-21 12:30"), dpi);
        using var withoutDate = Render(BuildTicket("1042", string.Empty), dpi);
        using var numberImage = Render(BuildTicket("1043", "2026-10-21 12:30"), dpi);

        var date = TicketInk.DiffBounds(withDate, withoutDate);

        Assert.NotNull(date);

        var number = TicketInk.DiffBounds(withDate, numberImage);

        Assert.NotNull(number);

        Assert.True(number!.Value.Bottom < date!.Value.Top,
            $"The date is not printed below the receipt number at {dpi} dpi.");
    }

    /// <summary>
    /// The two lines must not touch each other, not even at the smallest share
    /// that is still safe to print.
    /// </summary>
    [Theory]
    [InlineData(96, SafeLineSpacing)]
    [InlineData(96, 90)]
    [InlineData(96, 130)]
    [InlineData(203, SafeLineSpacing)]
    [InlineData(203, 90)]
    [InlineData(203, 130)]
    public void TheReceiptNumber_AndTheDate_DoNotTouch(int dpi, int lineSpacing)
    {
        using var baseImage = Render(BuildTicket("1042", "2026-10-21 12:30"), dpi, lineSpacing);
        using var numberImage = Render(BuildTicket("1043", "2026-10-21 12:30"), dpi, lineSpacing);
        using var dateImage = Render(BuildTicket("1042", "2026-10-21 12:31"), dpi, lineSpacing);

        var bands = TicketInk.InkBands(baseImage);

        var numberBand = FindBand(bands, TicketInk.DiffBounds(baseImage, numberImage));
        var dateBand = FindBand(bands, TicketInk.DiffBounds(baseImage, dateImage));

        Assert.True(numberBand >= 0, $"The receipt number is not printed at {dpi} dpi.");
        Assert.True(dateBand >= 0, $"The date is not printed at {dpi} dpi.");

        Assert.NotEqual(numberBand, dateBand);
    }

    /// <summary>
    /// Index of the band of ink the box belongs to, or -1 when the box is not
    /// inside a band.
    /// </summary>
    private static int FindBand(List<(int Top, int Bottom)> bands, Rectangle? box)
    {
        if (box == null) return -1;

        for (var i = 0; i < bands.Count; i++)
        {
            if (bands[i].Top <= box.Value.Top && bands[i].Bottom >= box.Value.Bottom) return i;
        }

        return -1;
    }

    private static Bitmap Render(TicketPrintModel model, int dpi, int lineSpacing = TicketPrintConfig.DefaultLineSpacing)
    {
        // The paper has a fixed physical size, so the whole ticket grows with the
        // resolution of the device.
        const float width = 283f;
        const float height = 1169f;

        var scale = dpi / 96f;

        var image = new Bitmap((int)(width * scale), (int)(height * scale));
        image.SetResolution(dpi, dpi);

        using var printer = new ThermalPrinter(new FixedConfigStore(lineSpacing));

        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);
            printer.RenderTicket(g, model, new RectangleF(20f * scale, 20f * scale, width * scale - 40f * scale, image.Height - 40f * scale));
        }

        return image;
    }

    private static TicketPrintModel BuildTicket(string ticketNumber, string date)
    {
        var model = new TicketPrintModel
        {
            Title = "كافتيريا التميز",
            Subtitle = "شارع النيل الخرطوم",
            SectionName = "قسم المشروبات",
            Phone = "+249 123 456 789",
            TicketNumber = ticketNumber,
            Date = date,
            CustomerName = "محمد علي",
            CustomerPhone = "0912345678",
            PaymentMethod = "نقداً",
            PickupTime = "12:45",
            Total = 125.50m,
            CurrencySymbolAr = "ج.س",
            OrderId = "ORD-1042"
        };

        model.Lines.Add(new TicketLine { Text = "2x أقاشي لحم", PriceText = "12.50" });
        model.Lines.Add(new TicketLine { Text = "1x شيش طاووق", PriceText = "12.50" });

        return model;
    }

    private sealed class FixedConfigStore : ITicketConfigStore
    {
        private readonly TicketPrintConfig _config;

        public FixedConfigStore(int lineSpacing)
        {
            _config = new TicketPrintConfig { TicketLineSpacing = lineSpacing };
        }

        public string ConfigPath => "in-memory";

        public TicketPrintConfig Load() => _config;

        public void Save(TicketPrintConfig config) { }
    }
}
