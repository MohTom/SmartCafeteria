using SmartCafeteria.Core.Configuration;

namespace SmartCafeteria.Printing;

/// <summary>
/// Single source of truth for every font size and row height used when rendering
/// a ticket. Both are derived from one base size so they can never drift apart:
/// scaling the fonts without scaling the row heights clips the text on 80mm paper.
///
/// The reference base size (9) reproduces the original hard-coded values exactly.
/// The metrics below are the layout at 96 dpi; <see cref="TicketLayout"/> scales
/// them to the resolution of the device the ticket is printed on.
/// </summary>
public sealed class TicketFontSet
{
    /// <summary>Base size the layout was originally written against.</summary>
    public const float ReferenceBaseSize = 9f;

    /// <summary>
    /// Builds the metrics for a base font size and the distance between two rows.
    /// </summary>
    /// <param name="baseFontSize">Size of the fonts in points.</param>
    /// <param name="lineSpacingPercent">
    /// Distance between two rows, in percent of the height of the text box of one
    /// line. The default is the layout the receipt is printed with today.
    /// </param>
    public TicketFontSet(int baseFontSize, int lineSpacingPercent = TicketPrintConfig.DefaultLineSpacing)
    {
        BaseFontSize = baseFontSize;
        Scale = baseFontSize / ReferenceBaseSize;
        LineSpacingPercent = TicketPrintConfig.ClampLineSpacing(lineSpacingPercent);
        LineSpacingFactor = LineSpacingPercent / 100f;

        // Font sizes (points)
        Title = 12f * Scale;
        Section = 11f * Scale;
        Normal = 9f * Scale;
        Small = 8f * Scale;
        Bold = 9f * Scale;
        Total = 11f * Scale;

        // Text box heights (reference pixels at 96 dpi)
        TitleHeight = 20f * Scale;
        SectionHeight = 18f * Scale;
        LineHeight = 14f * Scale;
        TotalHeight = 16f * Scale;

        // Vertical advances (reference pixels at 96 dpi)
        TitleAdvance = 22f * Scale;
        SectionAdvance = 20f * Scale;
        LineAdvance = 14f * Scale;
        DetailAdvance = 16f * Scale;
        TotalAdvance = 18f * Scale;
        BlockGap = 4f * Scale;
        RuleGap = 6f * Scale;

        // Space kept free at the bottom of the page so the footer is never cut off
        MinimalFooterReserve = 40f * Scale;
        FullFooterReserve = 80f * Scale;
    }

    /// <summary>The base size this set was built from.</summary>
    public int BaseFontSize { get; }

    /// <summary>Ratio applied to every metric; 1.0 at the reference base size.</summary>
    public float Scale { get; }

    /// <summary>Distance between two rows in percent of the height of the text box.</summary>
    public int LineSpacingPercent { get; }

    /// <summary>Distance between two rows as a factor of the height of the text box.</summary>
    public float LineSpacingFactor { get; }

    // Font sizes (points)
    public float Title { get; }
    public float Section { get; }
    public float Normal { get; }
    public float Small { get; }
    public float Bold { get; }
    public float Total { get; }

    // Text box heights (reference pixels at 96 dpi)
    public float TitleHeight { get; }
    public float SectionHeight { get; }
    public float LineHeight { get; }
    public float TotalHeight { get; }

    // Vertical advances (reference pixels at 96 dpi)
    public float TitleAdvance { get; }
    public float SectionAdvance { get; }
    public float LineAdvance { get; }
    public float DetailAdvance { get; }
    public float TotalAdvance { get; }
    public float BlockGap { get; }
    public float RuleGap { get; }

    public float MinimalFooterReserve { get; }
    public float FullFooterReserve { get; }

    /// <summary>The layout used when nothing has been configured.</summary>
    public static TicketFontSet Default { get; } = new(TicketPrintConfig.DefaultFontSize);

    /// <summary>
    /// Builds the font set for the current config. Any failure (missing file,
    /// corrupt file, unreadable folder) falls back to the default layout rather
    /// than blocking the print.
    /// </summary>
    public static TicketFontSet FromConfig(ITicketConfigStore store)
    {
        if (store == null) return Default;

        try
        {
            var config = store.Load();
            return new TicketFontSet(config.TicketFontSize, config.TicketLineSpacing);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Ticket font configuration could not be loaded: {0}", ex);
            return Default;
        }
    }
}
