using System;
using System.Globalization;
using System.Windows.Data;
using WpfPlotApp.ViewModels;

namespace WpfPlotApp.Converters;

public class DynamicPrecisionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double number)
        {
            // Получаем точность из контекста (будет передана как parameter)
            if (parameter is int precision)
            {
                return number.ToString($"F{Math.Max(0, Math.Min(10, precision))}", culture);
            }
            
            // Если точность не передана, используем стандартное форматирование
            return number.ToString("F2", culture);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (double.TryParse(value?.ToString(), NumberStyles.Float, culture, out double result))
        {
            return result;
        }
        
        return 0.0;
    }
} 