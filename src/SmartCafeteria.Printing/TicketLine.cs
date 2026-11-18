namespace SmartCafeteria.Printing;

public class TicketLine
{
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Price of the line, printed in its own column on the left margin.
    /// Empty for lines that have no price (customisations, notes, section tickets).
    /// </summary>
    public string PriceText { get; set; } = string.Empty;

    public bool IsBold { get; set; }
    public bool IsSmall { get; set; }
}
