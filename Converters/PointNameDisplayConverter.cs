using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfPlotApp.Converters
{
    public class PointNameDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                return $"Точка {name}";
            }
            return "Точка";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string displayName && displayName.StartsWith("Точка "))
            {
                return displayName.Substring(6); // Убираем "Точка " в начале
            }
            return value?.ToString() ?? "";
        }
    }
} 