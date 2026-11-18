using System.Drawing;

namespace SmartCafeteria.Services.Tests;

/// <summary>
/// Reads the ink of a rendered ticket. Both ticket test files need the same
/// measurements, so they live here instead of being copied twice.
/// </summary>
internal static class TicketInk
{
    /// <summary>
    /// Rows that carry the ink of a text drawn in a box of the given height,
    /// counted from the top of the box.
    /// </summary>
    public static List<int> InkRows(string text, Font font, float boxHeight, int dpi)
    {
        const float width = 600f;
        var height = (int)Math.Ceiling(boxHeight) + 4;

        var image = new Bitmap((int)width, height);
        image.SetResolution(dpi, dpi);

        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.White);

            using var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.DirectionRightToLeft
            };

            g.DrawString(text, font, Brushes.Black, new RectangleF(0f, 0f, width, boxHeight), format);
        }

        var rows = new List<int>();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (!HasInk(image, x, y)) continue;
                rows.Add(y);
                break;
            }
        }

        image.Dispose();
        return rows;
    }

    /// <summary>
    /// Bands of ink of a rendered image: every band starts on the first row that
    /// carries ink and ends on the last one before the next blank row.
    /// </summary>
    public static List<(int Top, int Bottom)> InkBands(Bitmap image)
    {
        var bands = new List<(int Top, int Bottom)>();
        var top = -1;

        for (var y = 0; y < image.Height; y++)
        {
            var inked = false;
            for (var x = 0; x < image.Width && !inked; x++)
            {
                inked = HasInk(image, x, y);
            }

            if (!inked)
            {
                if (top >= 0)
                {
                    bands.Add((top, y - 1));
                    top = -1;
                }

                continue;
            }

            if (top < 0) top = y;
        }

        if (top >= 0) bands.Add((top, image.Height - 1));

        return bands;
    }

    /// <summary>Last row of the image that carries ink, or -1 when it is empty.</summary>
    public static int LastInkRow(Bitmap image)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (HasInk(image, x, y)) return y;
            }
        }

        return -1;
    }

    /// <summary>Colour of a pixel that carries text. Anti-aliased edges are grey.</summary>
    public static bool HasInk(Bitmap image, int x, int y)
    {
        var color = image.GetPixel(x, y);
        return color.R < 250 || color.G < 250 || color.B < 250;
    }

    /// <summary>
    /// Two pixels are the same when their channels are the same. The colour
    /// operators cannot be used: a pixel read back from a bitmap never equals
    /// <see cref="Color.White"/> even when it is white.
    /// </summary>
    public static bool SamePixel(Color left, Color right)
        => left.R == right.R && left.G == right.G && left.B == right.B;

    /// <summary>
    /// Box around every pixel that is not the same in both images, or null when
    /// the two images are identical. Two receipts that differ in one value tell
    /// with this where that value was printed.
    /// </summary>
    public static Rectangle? DiffBounds(Bitmap left, Bitmap right)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < left.Height; y++)
        {
            for (var x = 0; x < left.Width; x++)
            {
                if (SamePixel(left.GetPixel(x, y), right.GetPixel(x, y))) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        return maxX < 0 ? null : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
