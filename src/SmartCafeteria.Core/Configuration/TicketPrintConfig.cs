namespace SmartCafeteria.Core.Configuration;

/// <summary>
/// User preferences that are stored OUTSIDE the database (JSON config file),
/// so the deployed SQLite schema never has to change to add a preference.
/// </summary>
public class TicketPrintConfig
{
    /// <summary>Base font size (points) that reproduces the original ticket layout exactly.</summary>
    public const int DefaultFontSize = 9;

    public const int MinFontSize = 7;
    public const int MaxFontSize = 16;

    /// <summary>Base font size used for every ticket (customer receipt, cashier receipt, section tickets).</summary>
    public int TicketFontSize { get; set; } = DefaultFontSize;

    /// <summary>
    /// Distance between two lines of the ticket, in percent of the height of the
    /// text box of one line. 100 % would print every line in its own full box
    /// (the layout before the spacing was tightened), a smaller value pulls the
    /// lines closer together.
    /// </summary>
    public int TicketLineSpacing { get; set; } = DefaultLineSpacing;

    public bool EnforceInventoryTracking { get; set; } = true;

    /// <summary>
    /// Share of the line box used as the distance between two rows. 90 % is the
    /// layout the receipt is printed with today: the characters of a line never
    /// change, only the white space between two lines does.
    /// </summary>
    public const int DefaultLineSpacing = 90;

    /// <summary>
    /// Smallest share the user can choose. The characters of a line never
    /// change, only the distance between two rows does, but below 75 % the lines
    /// of the receipt are printed on top of each other (the value was measured
    /// on the ink of the real ticket lines: at 96 dpi with a 13 pt font two
    /// product names that follow each other directly already touch at 70 %).
    /// The smaller shares are for a receipt that has to be as short as possible.
    /// </summary>
    public const int MinLineSpacing = 10;

    /// <summary>
    /// Largest share. A bigger distance only separates the lines, but a receipt
    /// with a huge distance does not fit on the paper any more.
    /// </summary>
    public const int MaxLineSpacing = 130;

    /// <summary>Keeps a hand-edited or corrupt value inside the printable range.</summary>
    public static int ClampFontSize(int value) => Math.Clamp(value, MinFontSize, MaxFontSize);

    /// <summary>Keeps a hand-edited or corrupt value inside the printable range.</summary>
    public static int ClampLineSpacing(int value) => Math.Clamp(value, MinLineSpacing, MaxLineSpacing);
}
