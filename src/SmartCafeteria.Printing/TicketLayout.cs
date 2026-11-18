using System.Drawing;

namespace SmartCafeteria.Printing;

/// <summary>
/// Resolves the geometry of one ticket for the device it is printed on: the fonts,
/// the height of every text box and the vertical distance between two rows.
/// </summary>
/// <remarks>
/// <para>
/// The original layout hard-coded every row height in pixels while the fonts were
/// sized in points. On a 203 dpi thermal printer one 14 px row box only kept the
/// top half of a 9 pt line, so the bottom of the characters never reached the paper.
/// </para>
/// <para>
/// Every metric below is therefore the reference metric of
/// <see cref="TicketFontSet"/> scaled to the resolution of the target device and
/// never smaller than the height the font actually needs. The reference metrics
/// stay untouched: they still describe the layout at 96 dpi and are scaled to the
/// device, so a ticket printed on a screen-sized preview and a ticket printed on
/// the thermal printer keep the same physical proportions.
/// </para>
/// <para>
/// The distance between two rows is shorter than the line box: the leading the
/// font adds above and below the characters is never printed, so the rows can be
/// placed closer together without touching each other.
/// </para>
/// </remarks>
public sealed class TicketLayout : IDisposable
{
    private const float ReferenceDpi = 96f;
    private const float ReferencePriceGap = 6f;

    /// <summary>Area used to measure a string without wrapping it.</summary>
    public static readonly SizeF MeasurementArea = new(4096f, 4096f);

    public TicketLayout(Graphics graphics, TicketFontSet fonts)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(fonts);

        DpiFactor = graphics.DpiY > 0f ? graphics.DpiY / ReferenceDpi : 1f;

        TitleFont = new Font("Segoe UI", fonts.Title, FontStyle.Bold);
        SectionFont = new Font("Segoe UI", fonts.Section, FontStyle.Bold);
        NormalFont = new Font("Segoe UI", fonts.Normal, FontStyle.Regular);
        SmallFont = new Font("Segoe UI", fonts.Small, FontStyle.Regular);
        BoldFont = new Font("Segoe UI", fonts.Bold, FontStyle.Bold);
        TotalFont = new Font("Segoe UI", fonts.Total, FontStyle.Bold);

        // A text box must never be shorter than the line height of the font it
        // holds, otherwise GDI+ clips the bottom of the characters.
        LineHeight = Math.Max(fonts.LineHeight * DpiFactor, FontHeight(graphics, NormalFont, SmallFont, BoldFont));

        // The distance between two rows is shorter than the line box: the leading
        // the font adds above and below the characters is not printed, so the rows
        // can be moved closer together without touching each other. How much of
        // the line box is used is the share the user chose in the settings.
        RowPitchFactor = fonts.LineSpacingFactor;
        LineAdvance = LineHeight * RowPitchFactor;
        DetailAdvance = Math.Max(fonts.DetailAdvance * DpiFactor, LineHeight) * RowPitchFactor;

        TitleHeight = Math.Max(fonts.TitleHeight * DpiFactor, TitleFont.GetHeight(graphics));
        TitleAdvance = Math.Max(fonts.TitleAdvance * DpiFactor, TitleHeight);

        SectionHeight = Math.Max(fonts.SectionHeight * DpiFactor, SectionFont.GetHeight(graphics));
        SectionAdvance = Math.Max(fonts.SectionAdvance * DpiFactor, SectionHeight);

        TotalHeight = Math.Max(fonts.TotalHeight * DpiFactor, TotalFont.GetHeight(graphics));
        TotalAdvance = Math.Max(fonts.TotalAdvance * DpiFactor, TotalHeight);

        BlockGap = fonts.BlockGap * DpiFactor;
        RuleGap = fonts.RuleGap * DpiFactor;
        PriceGap = ReferencePriceGap * DpiFactor;

        // Space kept free at the bottom of the page so the footer is never cut
        // off. It covers the advance of the last ordered item, the line that
        // marks the items that did not fit and the whole footer block.
        MinimalFooterReserve = Math.Max(
            fonts.MinimalFooterReserve * DpiFactor,
            (LineAdvance * 2f) + LineHeight);

        FullFooterReserve = Math.Max(
            fonts.FullFooterReserve * DpiFactor,
            (LineAdvance * 3f) + LineHeight + BlockGap + RuleGap + TotalAdvance + DetailAdvance);
    }

    /// <summary>Ratio between the resolution of the device and the reference 96 dpi.</summary>
    public float DpiFactor { get; }

    /// <summary>
    /// Share of the line box that is used as the distance between two rows. The
    /// line box itself keeps the full height of the font, so the characters of a
    /// line are not changed in any way; only the white space between two lines
    /// shrinks. The share comes from the settings of the user and is limited to
    /// the range that was measured on the ink of the real ticket lines.
    /// </summary>
    public float RowPitchFactor { get; }

    public Font TitleFont { get; }
    public Font SectionFont { get; }
    public Font NormalFont { get; }
    public Font SmallFont { get; }
    public Font BoldFont { get; }
    public Font TotalFont { get; }

    public float TitleHeight { get; }
    public float TitleAdvance { get; }
    public float SectionHeight { get; }
    public float SectionAdvance { get; }
    public float LineHeight { get; }
    public float LineAdvance { get; }
    public float DetailAdvance { get; }
    public float TotalHeight { get; }
    public float TotalAdvance { get; }
    public float BlockGap { get; }
    public float RuleGap { get; }

    /// <summary>
    /// Distance from a text row to a rule drawn below it. The rule belongs to the
    /// bottom of the line box and not to the row pitch, so it never touches the
    /// characters of the row above it.
    /// </summary>
    public float RuleAdvance => LineHeight;

    /// <summary>Gap between the price column and the item name.</summary>
    public float PriceGap { get; }

    public float MinimalFooterReserve { get; }
    public float FullFooterReserve { get; }

    /// <summary>Font of a ticket line, honouring the bold and small flags.</summary>
    public Font FontFor(TicketLine line)
    {
        if (line.IsBold) return BoldFont;
        return line.IsSmall ? SmallFont : NormalFont;
    }

    private static float FontHeight(Graphics graphics, params Font[] fonts)
    {
        var height = 0f;
        foreach (var font in fonts)
        {
            height = Math.Max(height, font.GetHeight(graphics));
        }

        return height;
    }

    public void Dispose()
    {
        TitleFont.Dispose();
        SectionFont.Dispose();
        NormalFont.Dispose();
        SmallFont.Dispose();
        BoldFont.Dispose();
        TotalFont.Dispose();
    }
}
