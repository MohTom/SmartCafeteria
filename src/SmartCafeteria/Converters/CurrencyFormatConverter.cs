using System.Globalization;
using System.Windows.Data;

namespace SmartCafeteria.Converters;
public class CurrencyFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is decimal amount && values[1] is string currency)
        {
            return $"{amount:F2} {currency}";
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
