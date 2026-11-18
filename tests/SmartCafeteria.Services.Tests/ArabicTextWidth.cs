using System.Drawing;
using System.Drawing.Text;

namespace SmartCafeteria.Services.Tests;

/// <summary>Measures Arabic text in the font the PDF reports are printed with, so a
/// test can tell whether a label fits the column it is printed in.</summary>
internal sealed class ArabicTextWidth : IDisposable
{
    private const string CairoResource = "SmartCafeteria.Services.Fonts.Cairo.ttf";

    private readonly PrivateFontCollection _fonts = new();
    private readonly Bitmap _canvas = new(4, 4);
    private readonly Graphics _graphics;
    private readonly string _fontPath;

    private ArabicTextWidth(string fontPath, float sizeInPoints)
    {
        _fontPath = fontPath;
        _fonts.AddFontFile(fontPath);

        _graphics = Graphics.FromImage(_canvas);
        _graphics.PageUnit = GraphicsUnit.Point;
        _graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

        Font = new Font(_fonts.Families[0], sizeInPoints, FontStyle.Bold, GraphicsUnit.Point);
    }

    /// <summary>The font the measurement is taken in.</summary>
    public Font Font { get; }

    /// <summary>Width of the text in points, shaped the way the PDF shapes it.</summary>
    public double Measure(string text)
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;

        try
        {
            return _graphics.MeasureString(text, Font, new SizeF(float.MaxValue, float.MaxValue), format).Width;
        }
        finally
        {
            format.Dispose();
        }
    }

    /// <summary>Measures in the Cairo font the reports embed.</summary>
    public static ArabicTextWidth Cairo(float sizeInPoints)
    {
        var path = Path.Combine(Path.GetTempPath(), $"Cairo_{Guid.NewGuid():N}.ttf");

        using (var resource = typeof(AnalyticsPdfService).Assembly.GetManifestResourceStream(CairoResource))
        using (var file = File.Create(path))
            resource!.CopyTo(file);

        return new ArabicTextWidth(path, sizeInPoints);
    }

    public void Dispose()
    {
        Font.Dispose();
        _graphics.Dispose();
        _canvas.Dispose();
        _fonts.Dispose();

        try
        {
            File.Delete(_fontPath);
        }
        catch (IOException)
        {
        }
    }
}
