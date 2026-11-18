using System.Drawing;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Guards the two defects that were fixed in the printed receipt:
/// <list type="bullet">
/// <item>the ordered items and the total were anchored on the left margin,</item>
/// <item>the text boxes were shorter than the font, so on a 203 dpi thermal
/// printer the bottom half of every character was cut off.</item>
/// </list>
/// The suite that compared the ticket with the original layout pixel by pixel was
/// removed together with the defect: that layout is exactly what these tests
/// forbid, so it can no longer be the reference.
/// </summary>
public class TicketRenderingTests
{
    private const float PageWidth = 320f;
    private const float PageHeight = 700f;

    /// <summary>How far the ink may stay away from the right margin.</summary>
    private const int RightMarginTolerance = 6;

    /// <summary>How far the ink may stay away from the left margin.</summary>
    private const int LeftMarginTolerance = 8;

    [Fact]
    public void OrderedItems_AreAnchoredOnTheRightMargin()
    {
        var model = BuildTicket(minimal: false);
        var withoutFirstItem = BuildTicket(minimal: false);
        withoutFirstItem.Lines[0].Text = string.Empty;
        withoutFirstItem.Lines[0].PriceText = string.Empty;

        using var actual = Render(model);
        using var without = Render(withoutFirstItem);

        var ink = TicketInk.DiffBounds(actual, without);
        Assert.NotNull(ink);

        Assert.True(ink.Value.Right >= PageWidth - RightMarginTolerance,
            $"The ordered items end at x={ink.Value.Right} instead of on the right margin ({PageWidth}).");
    }

    [Fact]
    public void OrderType_IsPrintedOnTheSectionTicket()
    {
        var withType = BuildTicket(minimal: true);
        withType.OrderType = "سفري";
        var withoutType = BuildTicket(minimal: true);
        withoutType.OrderType = string.Empty;

        using var actual = Render(withType);
        using var without = Render(withoutType);

        Assert.NotNull(TicketInk.DiffBounds(actual, without));
    }

    [Fact]
    public void OrderedItems_OfASectionTicket_AreAnchoredOnTheRightMargin()
    {
        var model = BuildTicket(minimal: true);
        var withoutFirstItem = BuildTicket(minimal: true);
        withoutFirstItem.Lines[0].Text = string.Empty;

        using var actual = Render(model);
        using var without = Render(withoutFirstItem);

        var ink = TicketInk.DiffBounds(actual, without);
        Assert.NotNull(ink);

        Assert.True(ink.Value.Right >= PageWidth - RightMarginTolerance,
            $"The ordered items end at x={ink.Value.Right} instead of on the right margin ({PageWidth}).");
        Assert.True(ink.Value.Left > RightMarginTolerance,
            $"The ordered items still start at the left margin (x={ink.Value.Left}).");
    }

    [Fact]
    public void ItemPrices_ArePrintedInTheirOwnColumnOnTheLeftMargin()
    {
        // Two prices of a different width: the column is as wide as the widest of
        // them and both amounts end at the same place.
        var model = BuildTicket(minimal: false, lineCount: 3);
        model.Lines[0].PriceText = "110.50";
        model.Lines[1].PriceText = "110.50";
        model.Lines[2].PriceText = "9.50";

        var withoutFirstPrice = BuildTicket(minimal: false, lineCount: 3);
        withoutFirstPrice.Lines[0].PriceText = "110.50";
        withoutFirstPrice.Lines[1].PriceText = "110.50";
        withoutFirstPrice.Lines[2].PriceText = "9.50";
        withoutFirstPrice.Lines[0].PriceText = string.Empty;

        var withoutLastPrice = BuildTicket(minimal: false, lineCount: 3);
        withoutLastPrice.Lines[0].PriceText = "110.50";
        withoutLastPrice.Lines[1].PriceText = "110.50";
        withoutLastPrice.Lines[2].PriceText = "9.50";
        withoutLastPrice.Lines[2].PriceText = string.Empty;

        using var actual = Render(model);
        using var first = Render(withoutFirstPrice);
        using var last = Render(withoutLastPrice);

        var widePrice = TicketInk.DiffBounds(actual, first);
        var narrowPrice = TicketInk.DiffBounds(actual, last);
        Assert.NotNull(widePrice);
        Assert.NotNull(narrowPrice);

        // The column of the prices sits on the left margin ...
        Assert.True(widePrice.Value.Left <= LeftMarginTolerance,
            $"The price column starts at x={widePrice.Value.Left} instead of on the left margin.");

        // ... the amounts end at the same x, so they line up ...
        Assert.True(Math.Abs(widePrice.Value.Right - narrowPrice.Value.Right) <= 1,
            $"The prices do not line up: {widePrice.Value.Right} and {narrowPrice.Value.Right}.");

        // ... and the price is in a column of its own, far away from the name.
        Assert.True(narrowPrice.Value.Right < PageWidth / 2f,
            $"The price ends at x={narrowPrice.Value.Right} and is not in a column of its own.");
    }

    [Fact]
    public void TheTotal_IsAnchoredOnTheRightMargin()
    {
        var model = BuildTicket(minimal: false);
        model.Total = 125.50m;
        var withOtherTotal = BuildTicket(minimal: false);
        withOtherTotal.Total = 0.00m;

        using var actual = Render(model);
        using var other = Render(withOtherTotal);

        // Only the amount of the total differs, so the changed pixels sit on the
        // line of the total and mark where that line is.
        var changed = TicketInk.DiffBounds(actual, other);
        Assert.NotNull(changed);

        var line = InkBoundsInRows(actual, changed.Value.Top, changed.Value.Bottom);
        Assert.NotNull(line);

        Assert.True(line.Value.Right >= PageWidth - RightMarginTolerance,
            $"The total ends at x={line.Value.Right} instead of on the right margin ({PageWidth}).");
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    [InlineData(300)]
    public void RowBoxes_AreTallEnoughForTheFont(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        Assert.True(layout.LineHeight >= layout.NormalFont.GetHeight(g), "The row box is shorter than the font.");
        Assert.True(layout.LineHeight >= layout.SmallFont.GetHeight(g), "The row box is shorter than the small font.");
        Assert.True(layout.LineHeight >= layout.BoldFont.GetHeight(g), "The row box is shorter than the bold font.");
        Assert.True(layout.TitleHeight >= layout.TitleFont.GetHeight(g), "The title box is shorter than the title font.");
        Assert.True(layout.SectionHeight >= layout.SectionFont.GetHeight(g), "The section box is shorter than the section font.");
        Assert.True(layout.TotalHeight >= layout.TotalFont.GetHeight(g), "The total box is shorter than the total font.");
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    [InlineData(300)]
    public void ItemText_IsNotCutOff(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        // The names of the products the cafeteria sells: the Arabic descenders
        // reach the lowest pixel of the line box.
        foreach (var text in new[] { "2x قهوة", "1x شاورما دجاج", "2x ميلك شيك فانيليا", "3x كرواسان" })
        {
            var inRow = TicketInk.InkRows(text, layout.NormalFont, layout.LineHeight, dpi);
            var withoutLimit = TicketInk.InkRows(text, layout.NormalFont, layout.LineHeight * 6f, dpi);

            Assert.Equal(withoutLimit.Count, inRow.Count);
            Assert.Equal(withoutLimit, inRow);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderTicket_AtALargerFontSize_KeepsEveryLineInsideTheFooterReserve(bool minimal)
    {
        var model = BuildTicket(minimal, lineCount: 60);

        // At the largest size the footer must still be fully visible: the last
        // "*******" line has to sit above the bottom of the page. The canvas is
        // twice as tall as the page, so ink that falls outside the page is seen.
        using var image = Render(model, TicketPrintConfig.MaxFontSize, canvasHeight: PageHeight * 2);
        var lastInk = TicketInk.LastInkRow(image);

        Assert.True(lastInk < PageHeight,
            $"The footer was cut off at font size {TicketPrintConfig.MaxFontSize} (last ink row {lastInk}).");
    }

    [Fact]
    public void RenderTicket_AtALargerFontSize_ProducesADifferentTicket()
    {
        var model = BuildTicket(minimal: false);

        using var atReference = Render(model, TicketPrintConfig.DefaultFontSize);
        using var atLargest = Render(model, TicketPrintConfig.MaxFontSize);

        Assert.False(ImagesEqual(atReference, atLargest),
            "Raising the ticket font size did not change the rendered ticket.");
    }

    private static Bitmap Render(TicketPrintModel model, int fontSize = TicketPrintConfig.DefaultFontSize, float? canvasHeight = null)
    {
        var image = new Bitmap((int)PageWidth, (int)(canvasHeight ?? PageHeight));

        var store = new FixedConfigStore(fontSize);
        using var printer = new ThermalPrinter(store);

        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);
            printer.RenderTicket(g, model, new RectangleF(0f, 0f, PageWidth, PageHeight));
        }

        return image;
    }

    private static Rectangle? InkBoundsInRows(Bitmap image, int top, int bottom)
    {
        var minX = int.MaxValue;
        var maxX = -1;

        for (var y = Math.Max(0, top); y <= Math.Min(image.Height - 1, bottom); y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (!TicketInk.HasInk(image, x, y)) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
        }

        return maxX < 0 ? null : new Rectangle(minX, top, maxX - minX + 1, bottom - top + 1);
    }

    private static bool ImagesEqual(Bitmap left, Bitmap right)
    {
        if (left.Width != right.Width || left.Height != right.Height) return false;

        for (var y = 0; y < left.Height; y++)
        {
            for (var x = 0; x < left.Width; x++)
            {
                if (!TicketInk.SamePixel(left.GetPixel(x, y), right.GetPixel(x, y))) return false;
            }
        }

        return true;
    }

    private static TicketPrintModel BuildTicket(bool minimal, int lineCount = 3)
    {
        var model = new TicketPrintModel
        {
            Title = "كافتيريا التميز",
            Subtitle = "شارع النيل الخرطوم",
            SectionName = "قسم المشروبات",
            Phone = "+249 123 456 789",
            TicketNumber = "1042",
            Date = "2026-10-21 12:30",
            CustomerName = "محمد علي",
            CustomerPhone = "0912345678",
            PaymentMethod = "نقداً",
            PickupTime = "12:45",
            Total = 125.50m,
            CurrencySymbolAr = "ج.س",
            OrderId = "ORD-1042",
            IsMinimal = minimal
        };

        for (var i = 1; i <= lineCount; i++)
        {
            model.Lines.Add(new TicketLine
            {
                Text = $"صنف رقم {i}",
                PriceText = minimal ? string.Empty : $"{10 + i}.50"
            });
        }

        return model;
    }

    private sealed class FixedConfigStore : ITicketConfigStore
    {
        private readonly TicketPrintConfig _config;

        public FixedConfigStore(int fontSize)
        {
            _config = new TicketPrintConfig { TicketFontSize = fontSize };
        }

        public string ConfigPath => "in-memory";

        public TicketPrintConfig Load() => _config;

        public void Save(TicketPrintConfig config) { }
    }
}
