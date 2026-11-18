using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartCafeteria.Converters;
public class StatusToPreparingVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value switch
        {
            Core.Enums.OrderStatus orderStatus => orderStatus.ToString(),
            string s => s,
            _ => null
        };
        return status == "Received" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
