using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartCafeteria.Converters;
public class StockLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int[] stock && stock.Length == 2)
        {
            int inventory = stock[0];
            int maxInventory = stock[1];
            double ratio = maxInventory > 0 ? (double)inventory / maxInventory : 0;

            if (inventory == 0) return new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F));
            if (ratio <= 0.2) return new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00));
            return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}