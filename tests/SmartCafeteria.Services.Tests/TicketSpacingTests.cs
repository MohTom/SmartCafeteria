using System.Drawing;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Printing;
using Xunit;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Guards the vertical spacing of the printed receipt: the white space between two
/// lines was too large, but the characters of a line must never change, never be
/// clipped and never touch the line above or below it.
/// </summary>
public class TicketSpacingTests
{
    /// <summary>
    /// The names of the products the cafeteria sells. They are the worst case of
    /// the receipt: a name reaches the lowest pixel of its line box (the Arabic
    /// descenders) while a customization line starts a few pixels higher inside
    /// its own box.
    /// </summary>
    private static readonly string[] ProductNames =
    {
        "2x أقاشي لحم", "1x شيش طاووق", "2x كفتة مشوية", "1x شاورما دجاج",
        "3x ساندويتش طعمية", "1x فول مدمس", "2x فتة فول", "1x عصير فراولة",
        "1x عصير مانجو", "2x كركديه", "1x كوكتيل طبقات", "3x شاي سادة",
        "1x شاي لبن", "2x قهوة جبنة", "1x زنجبيل", "2x ميلك شيك فانيليا",
        "1x آيس كوفي", "3x لقيمات", "1x بسبوسة", "2x كنافة",
        "1x سمبوسة لحم", "2x فطيرة نوتيلا", "1x قهوة", "2x عصير برتقال"
    };

    /// <summary>
    /// The customization lines under a product name. They are printed in the
    /// smaller font.
    /// </summary>
    private static readonly string[] Customizations =
    {
        "  + حليب", "  + سكر زائد", "  بدون بصل", "  ملاحظة: بدون بصل",
        "  + جبنة", "  بارد بدون ثلج"
    };

    /// <summary>
    /// Smallest share of the line box at which two lines of the receipt never
    /// touch each other. The value comes from the ink measured on the rendered
    /// receipt: at 96 dpi the product names already grow into one block at 80 %
    /// (measured for the font sizes 7, 8 and 9 pt), and 85 % is the first share
    /// that still keeps every name apart for every font size of the settings
    /// slider and for every resolution of the printers.
    /// </summary>
    private const int SafeLineSpacing = 85;

    /// <summary>Resolutions of the devices the receipt is printed on.</summary>
    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    [InlineData(300)]
    public void RowPitch_IsSmallerThanTheFontLineHeight(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        // The rows must be closer together than the line box of the font ...
        Assert.True(layout.LineAdvance < layout.LineHeight,
            $"The distance between two rows ({layout.LineAdvance}) is not smaller than the line box ({layout.LineHeight}).");

        // ... but not so close that the characters of two rows touch.
        Assert.True(layout.LineAdvance >= layout.LineHeight * 0.85f,
            $"The distance between two rows ({layout.LineAdvance}) leaves no room for the characters of two rows.");
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    [InlineData(300)]
    public void ConsecutiveRows_StaySeparated(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        // The product name is printed in the normal font, the customization in
        // the smaller one. The name reaches the lowest pixel of its box and the
        // customization starts a few pixels higher: this is the pair of lines
        // with the least room between them.
        foreach (var name in ProductNames)
        {
            foreach (var customization in Customizations)
            {
                var nameInk = InkExtent(name, layout.NormalFont, layout.LineHeight, dpi);
                var customizationInk = InkExtent(customization, layout.SmallFont, layout.LineHeight, dpi);

                // The next row starts one pitch lower and puts its ink at its own
                // place inside the line box.
                var gap = layout.LineAdvance + customizationInk.Start - nameInk.End;

                Assert.True(gap >= 1,
                    $"The line \"{customization.Trim()}\" touches \"{name}\" at {dpi} dpi (gap {gap:F2} px).");
            }
        }
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    [InlineData(300)]
    public void TheRows_AreCloserThanTheFontLineHeight(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        // Four rows of the same text: the distance between the first and the
        // last one is three times the distance between two rows.
        const int count = 4;
        var starts = InkStartRows(layout, "2x عصير برتقال", count, dpi);

        var distance = starts[^1] - starts[0];

        // The rows are placed on the device pixels, so one pixel of rounding is
        // allowed.
        Assert.True(Math.Abs((count - 1) * layout.LineAdvance - distance) <= 1.5,
            $"The distance between the rows is {distance} px instead of {(count - 1) * layout.LineAdvance:F2} px.");
        Assert.True(distance < (count - 1) * layout.LineHeight,
            "The rows are not closer than the height of the font.");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(16)]
    public void ThePitch_ScalesWithTheFontSize(int baseFontSize)
    {
        foreach (var dpi in new[] { 96, 203, 300 })
        {
            using var image = new Bitmap(8, 8);
            image.SetResolution(dpi, dpi);
            using var g = Graphics.FromImage(image);

            using var layout = new TicketLayout(g, new TicketFontSet(baseFontSize));

            var ratio = layout.LineAdvance / layout.LineHeight;

            Assert.True(ratio >= 0.85f && ratio <= 0.95f,
                $"The distance between two rows is {ratio:F3} of the line box at {dpi} dpi and size {baseFontSize}.");
        }
    }

    /// <summary>
    /// Renders the whole receipt and checks that no two lines of characters
    /// touch each other. This is the check that the layout of the header, the
    /// rules, the items and the footer belongs together.
    /// </summary>
    /// <remarks>
    /// The check runs for every share that is safe to print. The user can
    /// choose a smaller share, and then the lines overlap on purpose.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Receipts))]
    public void TheRenderedReceipt_HasNoTouchingLines(int dpi, bool minimal, int lineSpacing)
    {
        using var image = Render(BuildTicket(minimal), dpi, TicketPrintConfig.DefaultFontSize, lineSpacing);
        var bands = TicketInk.InkBands(image);

        for (var i = 1; i < bands.Count; i++)
        {
            var gap = bands[i].Top - bands[i - 1].Bottom - 1;

            Assert.True(gap >= 1,
                $"The lines {i - 1} and {i} touch at {dpi} dpi (row {bands[i - 1].Bottom} and {bands[i].Top}).");
        }
    }

    /// <summary>
    /// The distance between two rows is the share of the line box the user chose.
    /// The smallest share is part of the check: even when the lines overlap, the
    /// distance between two rows is exactly the share the user picked.
    /// </summary>
    [Theory]
    [MemberData(nameof(SelectablePitches))]
    public void ThePitch_FollowsTheConfiguredSpacing(int dpi, int lineSpacing)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, new TicketFontSet(TicketPrintConfig.DefaultFontSize, lineSpacing));

        var ratio = layout.LineAdvance / layout.LineHeight;

        Assert.Equal(lineSpacing / 100f, ratio, 3);
    }

    /// <summary>
    /// Receipts to render: every resolution, both kinds of ticket and every
    /// share of the line box that is still safe to print. The smallest and the
    /// default share can be the same number, so the values are made unique.
    /// </summary>
    public static IEnumerable<object[]> Receipts()
    {
        foreach (var (dpi, minimal, lineSpacing) in SafeCases())
        {
            yield return new object[] { dpi, minimal, lineSpacing };
        }
    }

    /// <summary>Resolutions and every safe share of the line box.</summary>
    public static IEnumerable<object[]> SafePitches()
    {
        foreach (var (dpi, lineSpacing) in SafeSpacings())
        {
            yield return new object[] { dpi, lineSpacing };
        }
    }

    /// <summary>
    /// Resolutions and every share of the line box the user can choose. The
    /// smallest share is part of it: the distance between two rows must be
    /// exactly the share the user picked, also when the lines overlap.
    /// </summary>
    public static IEnumerable<object[]> SelectablePitches()
    {
        foreach (var (dpi, lineSpacing) in SelectableCases())
        {
            yield return new object[] { dpi, lineSpacing };
        }
    }

    /// <summary>
    /// The shares that are still safe to print: from the measured safe floor up
    /// to the largest share the user can choose.
    /// </summary>
    private static IEnumerable<(int Dpi, bool Minimal, int LineSpacing)> SafeCases()
    {
        foreach (var (dpi, lineSpacing) in SafeSpacings())
        {
            foreach (var minimal in new[] { false, true })
            {
                yield return (dpi, minimal, lineSpacing);
            }
        }
    }

    /// <summary>
    /// Every resolution and every share that is safe to print. The shares are
    /// made unique: the smallest and the default share can be the same number.
    /// </summary>
    private static IEnumerable<(int Dpi, int LineSpacing)> SafeSpacings()
    {
        var spacings = new[] { SafeLineSpacing, TicketPrintConfig.DefaultLineSpacing, TicketPrintConfig.MaxLineSpacing }
            .Distinct()
            .ToArray();

        foreach (var dpi in new[] { 96, 203 })
        {
            foreach (var lineSpacing in spacings)
            {
                yield return (dpi, lineSpacing);
            }
        }
    }

    /// <summary>Every share the user can choose, from the smallest to the largest.</summary>
    private static IEnumerable<(int Dpi, int LineSpacing)> SelectableCases()
    {
        var spacings = new[] { TicketPrintConfig.MinLineSpacing, TicketPrintConfig.DefaultLineSpacing, TicketPrintConfig.MaxLineSpacing }
            .Distinct()
            .ToArray();

        foreach (var dpi in new[] { 96, 203 })
        {
            foreach (var lineSpacing in spacings)
            {
                yield return (dpi, lineSpacing);
            }
        }
    }

    /// <summary>
    /// The default must not change the receipt: the rows are still placed at
    /// 90 % of the line box.
    /// </summary>
    [Fact]
    public void TheDefaultSpacing_KeepsTheCurrentLayout()
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(203, 203);
        using var g = Graphics.FromImage(image);
        using var layout = new TicketLayout(g, TicketFontSet.Default);

        Assert.Equal(0.9f, layout.LineAdvance / layout.LineHeight, 3);
        Assert.True(layout.LineAdvance < layout.LineHeight);
    }

    /// <summary>
    /// The characters of a line must never change, whatever share the user
    /// chooses: the line box keeps the full height of the font and the font
    /// itself is not resized. Only the distance between two rows shrinks.
    /// </summary>
    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void TheCharactersOfALine_AreNeverClipped_AtTheSmallestShare(int dpi)
    {
        using var image = new Bitmap(8, 8);
        image.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(image);

        using var smallest = new TicketLayout(g, new TicketFontSet(TicketPrintConfig.DefaultFontSize, TicketPrintConfig.MinLineSpacing));
        using var standard = new TicketLayout(g, new TicketFontSet(TicketPrintConfig.DefaultFontSize, TicketPrintConfig.DefaultLineSpacing));

        // The line box and the font are the same at every share.
        Assert.Equal(standard.LineHeight, smallest.LineHeight, 3);
        Assert.Equal(standard.NormalFont.Size, smallest.NormalFont.Size, 3);

        // Only the distance between two rows shrinks.
        Assert.Equal(TicketPrintConfig.MinLineSpacing / 100f, smallest.LineAdvance / smallest.LineHeight, 3);

        // The ink of the line is exactly the same at every share: the
        // characters are neither clipped nor resized.
        var smallestRows = TicketInk.InkRows(ProductNames[0], smallest.NormalFont, smallest.LineHeight, dpi);
        var standardRows = TicketInk.InkRows(ProductNames[0], standard.NormalFont, standard.LineHeight, dpi);

        Assert.NotEmpty(smallestRows);
        Assert.Equal(standardRows.Count, smallestRows.Count);
        Assert.Equal(standardRows[^1] - standardRows[0] + 1, smallestRows[^1] - smallestRows[0] + 1);
    }

    /// <summary>
    /// The reason the user can choose a smaller share: more of the receipt fits
    /// on the paper.
    /// </summary>
    [Fact]
    public void TheSmallestShare_MakesTheReceiptShorter()
    {
        using var smallest = Render(BuildTicket(false), 203, TicketPrintConfig.DefaultFontSize, TicketPrintConfig.MinLineSpacing);
        using var standard = Render(BuildTicket(false), 203, TicketPrintConfig.DefaultFontSize, TicketPrintConfig.DefaultLineSpacing);

        Assert.True(TicketInk.LastInkRow(smallest) < TicketInk.LastInkRow(standard),
            $"The receipt at the smallest share ends on row {TicketInk.LastInkRow(smallest)} instead of on row {TicketInk.LastInkRow(standard)}.");
    }

    /// <summary>
    /// Below the measured safe floor the lines of the receipt are printed on top
    /// of each other. That is the price of the smaller shares, and this test
    /// records it so it can never be mistaken for an accident.
    /// </summary>
    [Fact]
    public void TheSmallestShare_LetsTheLinesTouch_OnPurpose()
    {
        var names = ProductNames.Take(6).ToArray();

        var model = BuildTicket(false);
        model.Lines.Clear();
        foreach (var name in names)
        {
            model.Lines.Add(new TicketLine { Text = name, PriceText = "12.50" });
        }

        using var smallest = Render(model, 96, TicketPrintConfig.DefaultFontSize, TicketPrintConfig.MinLineSpacing);
        using var standard = Render(model, 96, TicketPrintConfig.DefaultFontSize, TicketPrintConfig.DefaultLineSpacing);

        var smallestRules = Rules(smallest);
        var standardRules = Rules(standard);

        var smallestBlocks = Blocks(smallest, smallestRules[1] + 1, smallestRules[2] - 1);
        var standardBlocks = Blocks(standard, standardRules[1] + 1, standardRules[2] - 1);

        // At the smallest share the product names grow into one block, at the
        // default share every name keeps its own.
        Assert.True(smallestBlocks.Count < standardBlocks.Count,
            $"The product names still keep their own block at the smallest share ({smallestBlocks.Count} instead of fewer than {standardBlocks.Count}).");
    }

    /// <summary>
    /// A bigger distance between the rows makes the receipt longer, so the
    /// footer can be pushed off the paper. It must stay on it.
    /// </summary>
    [Theory]
    [InlineData(96, false)]
    [InlineData(203, false)]
    [InlineData(96, true)]
    [InlineData(203, true)]
    public void TheFooterStaysOnThePage_AtTheMaximumSpacing(int dpi, bool minimal)
    {
        using var image = Render(BuildTicket(minimal), dpi, TicketPrintConfig.DefaultFontSize, TicketPrintConfig.MaxLineSpacing);

        var lastInkRow = TicketInk.LastInkRow(image);
        var printableBottom = image.Height - 20;

        Assert.True(lastInkRow < printableBottom,
            $"The footer ends on row {lastInkRow} and the paper ends on row {printableBottom}.");
    }

    /// <summary>
    /// Two product names that follow each other directly (a product without a
    /// customization line under it) are the pair with the least room: the ink of
    /// a name can fill its whole line box. Their ink must not grow into one
    /// block either.
    /// </summary>
    /// <remarks>
    /// The check runs for every share that is safe to print. Below the safe
    /// floor the lines overlap on purpose (see
    /// <see cref="TheSmallestShare_LetsTheLinesTouch_OnPurpose"/>).
    /// </remarks>
    [Theory]
    [MemberData(nameof(SafePitches))]
    public void TheRenderedReceipt_KeepsTwoProductNamesApart(int dpi, int lineSpacing)
    {
        var names = ProductNames.Take(6).ToArray();

        var model = BuildTicket(false);

        // Six product names without customization lines: every name follows the
        // previous one directly, and all of them fit on the paper.
        model.Lines.Clear();
        foreach (var name in names)
        {
            model.Lines.Add(new TicketLine { Text = name, PriceText = "12.50" });
        }

        using var image = Render(model, dpi, TicketPrintConfig.DefaultFontSize, lineSpacing);

        // The items are printed between the second and the third rule.
        var rules = Rules(image);

        Assert.True(rules.Count >= 3, $"The receipt has only {rules.Count} rules.");

        var blocks = Blocks(image, rules[1] + 1, rules[2] - 1);

        // One product name can print as more than one block: the marks above a
        // letter (the hamza of an alef) are not connected with the letter
        // itself. The number of blocks of all the names together is therefore
        // the sum of the blocks of every name on its own. When two names touch,
        // they grow into one block and that sum is smaller.
        var expected = 0;
        foreach (var name in names)
        {
            var single = BuildTicket(false);
            single.Lines.Clear();
            single.Lines.Add(new TicketLine { Text = name, PriceText = "12.50" });

            using var singleImage = Render(single, dpi, TicketPrintConfig.DefaultFontSize, lineSpacing);
            var singleRules = Rules(singleImage);

            expected += Blocks(singleImage, singleRules[1] + 1, singleRules[2] - 1).Count;
        }

        Assert.Equal(expected, blocks.Count);
    }

    /// <summary>
    /// A rule must not touch the characters of the line above it, otherwise the
    /// descenders of that line and the rule grow into one black block.
    /// </summary>
    [Theory]
    [InlineData(96, false)]
    [InlineData(203, false)]
    [InlineData(96, true)]
    [InlineData(203, true)]
    public void TheRenderedReceipt_KeepsTheRulesClearOfTheText(int dpi, bool minimal)
    {
        using var image = Render(BuildTicket(minimal), dpi, TicketPrintConfig.DefaultFontSize);

        for (var y = 1; y < image.Height; y++)
        {
            if (!IsRule(image, y)) continue;

            var above = false;
            for (var x = 0; x < image.Width && !above; x++)
            {
                above = TicketInk.HasInk(image, x, y - 1);
            }

            Assert.False(above, $"The rule on row {y} touches the characters above it at {dpi} dpi.");
        }
    }

    /// <summary>
    /// First and last inked row of a text inside a line box, counted from the top
    /// of the box. The end is exclusive.
    /// </summary>
    private static (int Start, int End) InkExtent(string text, Font font, float boxHeight, int dpi)
    {
        var rows = TicketInk.InkRows(text, font, boxHeight, dpi);

        Assert.NotEmpty(rows);
        return (rows[0], rows[^1] + 1);
    }

    /// <summary>
    /// Draws the same text in consecutive rows and returns the row where the ink
    /// of every one of them starts.
    /// </summary>
    private static List<int> InkStartRows(TicketLayout layout, string text, int count, int dpi)
    {
        const float width = 320f;
        var height = (int)Math.Ceiling(layout.LineHeight * 4f + layout.LineAdvance * count);

        var image = new Bitmap((int)width, height);
        image.SetResolution(dpi, dpi);

        // The line above reaches into the box of the next line, so the search for
        // the ink of a row starts where the text of that row puts it.
        var offset = InkExtent(text, layout.NormalFont, layout.LineHeight, dpi).Start;

        var starts = new List<int>();

        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);

            using var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap
            };

            var y = 0f;
            for (var i = 0; i < count; i++)
            {
                g.DrawString(text, layout.NormalFont, Brushes.Black, new RectangleF(0f, y, width, layout.LineHeight), format);

                var start = -1;
                for (var scan = (int)Math.Ceiling(y + offset); scan < image.Height && start < 0; scan++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        if (!TicketInk.HasInk(image, x, scan)) continue;
                        start = scan;
                        break;
                    }
                }

                starts.Add(start);
                y += layout.LineAdvance;
            }
        }

        image.Dispose();
        return starts;
    }

    /// <summary>Rows that are printed as a solid line from the left to the right margin.</summary>
    private static List<int> Rules(Bitmap image)
    {
        var rules = new List<int>();

        for (var y = 1; y < image.Height; y++)
        {
            if (IsRule(image, y)) rules.Add(y);
        }

        return rules;
    }

    /// <summary>
    /// Blocks of ink between two rows: every block starts on the first row that
    /// carries ink and ends on the last one before the next blank row.
    /// </summary>
    private static List<(int Top, int Bottom)> Blocks(Bitmap image, int top, int bottom)
    {
        var blocks = new List<(int Top, int Bottom)>();
        var start = -1;

        for (var y = top; y < bottom; y++)
        {
            var inked = false;
            for (var x = 0; x < image.Width && !inked; x++)
            {
                inked = TicketInk.HasInk(image, x, y);
            }

            if (!inked)
            {
                if (start < 0) continue;

                blocks.Add((start, y - 1));
                start = -1;
                continue;
            }

            if (start < 0) start = y;
        }

        if (start >= 0) blocks.Add((start, bottom - 1));

        return blocks;
    }

    /// <summary>A row that is printed as a solid line from the left to the right margin.</summary>
    private static bool IsRule(Bitmap image, int y)
    {
        var margin = image.Width / 12;
        var first = -1;
        var last = -1;

        for (var x = 0; x < image.Width; x++)
        {
            if (!TicketInk.HasInk(image, x, y)) continue;
            if (first < 0) first = x;
            last = x;
        }

        if (first < 0 || first > margin || last < image.Width - margin) return false;

        for (var x = first; x <= last; x++)
        {
            if (!TicketInk.HasInk(image, x, y)) return false;
        }

        return true;
    }

    private static Bitmap Render(TicketPrintModel model, int dpi, int fontSize, int lineSpacing = TicketPrintConfig.DefaultLineSpacing)
    {
        // The paper has a fixed physical size, so the whole ticket grows with
        // the resolution of the device. The width and the height are scaled with
        // the same factor, otherwise the ticket of a 203 dpi printer would be
        // squeezed into the width of a 96 dpi screen preview.
        const float width = 283f;
        const float height = 1169f;

        var scale = dpi / 96f;

        var image = new Bitmap((int)(width * scale), (int)(height * scale));
        image.SetResolution(dpi, dpi);

        using var printer = new ThermalPrinter(new FixedConfigStore(fontSize, lineSpacing));

        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);
            printer.RenderTicket(g, model, new RectangleF(20f * scale, 20f * scale, width * scale - 40f * scale, image.Height - 40f * scale));
        }

        return image;
    }

    private static TicketPrintModel BuildTicket(bool minimal)
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

        foreach (var name in ProductNames)
        {
            model.Lines.Add(new TicketLine { Text = name, PriceText = minimal ? string.Empty : "12.50" });
            model.Lines.Add(new TicketLine { Text = "  + حليب", IsSmall = true });
        }

        return model;
    }

    private sealed class FixedConfigStore : ITicketConfigStore
    {
        private readonly TicketPrintConfig _config;

        public FixedConfigStore(int fontSize, int lineSpacing = TicketPrintConfig.DefaultLineSpacing)
        {
            _config = new TicketPrintConfig { TicketFontSize = fontSize, TicketLineSpacing = lineSpacing };
        }

        public string ConfigPath => "in-memory";

        public TicketPrintConfig Load() => _config;

        public void Save(TicketPrintConfig config) { }
    }
}
