using System.Drawing;
using SmartCafeteria.Core.Configuration;
using SmartCafeteria.Printing;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Renders a ticket model to an in-memory bitmap, so the printing tests can look
/// at the ink of a ticket without a printer. Shared by the note-removal and the
/// cashier-summary suites.
/// </summary>
internal static class TicketRenderHarness
{
    public const float PageWidth = 320f;
    public const float PageHeight = 700f;

    public static Bitmap Render(
        TicketPrintModel model,
        int fontSize = TicketPrintConfig.DefaultFontSize,
        float? canvasHeight = null)
    {
        var image = new Bitmap((int)PageWidth, (int)(canvasHeight ?? PageHeight));

        var store = new FixedConfigStore(fontSize);
        using var printer = new ThermalPrinter(store);

        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.White);
            printer.RenderTicket(graphics, model, new RectangleF(0f, 0f, PageWidth, PageHeight));
        }

        return image;
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
