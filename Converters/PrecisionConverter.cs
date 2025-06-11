using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfPlotApp.Converters;

public class PrecisionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && 
            values[0] is double number && 
            values[1] is int precision)
        {
            return number.ToString($"F{Math.Max(0, Math.Min(10, precision))}", culture);
        }
        
        if (values.Length >= 2 && 
            values[0] is int intNumber && 
            values[1] is int intPrecision)
        {
            return ((double)intNumber).ToString($"F{Math.Max(0, Math.Min(10, intPrecision))}", culture);
        }

        return values[0]?.ToString() ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (double.TryParse(value?.ToString(), NumberStyles.Float, culture, out double result))
        {
            return new object[] { result, Binding.DoNothing };
        }
        
        return new object[] { 0.0, Binding.DoNothing };
    }
} 