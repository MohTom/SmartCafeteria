using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartCafeteria.Core.Enums;

namespace SmartCafeteria.Converters;
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value switch
        {
            OrderStatus orderStatus => orderStatus.ToString(),
            string s => s,
            _ => null
        };
        if (status != null)
        {
            return status switch
            {
                "Received" or "تم الاستلام" => new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)),
                "Cancelled" or "ملغي" => new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
