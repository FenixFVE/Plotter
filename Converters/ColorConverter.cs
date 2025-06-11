using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;
using ScottPlot;
using WpfPlotApp.Models;

namespace WpfPlotApp.Converters;

public class ColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScottPlot.Color scottPlotColor)
        {
            // Преобразуем ScottPlot.Color в System.Windows.Media.Color
            return System.Windows.Media.Color.FromArgb(
                scottPlotColor.A,
                scottPlotColor.R,
                scottPlotColor.G,
                scottPlotColor.B);
        }
        return System.Windows.Media.Colors.Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Windows.Media.Color mediaColor)
        {
            // Преобразуем System.Windows.Media.Color в ScottPlot.Color
            return new ScottPlot.Color(
                mediaColor.R,
                mediaColor.G,
                mediaColor.B,
                mediaColor.A);
        }
        return ScottPlot.Colors.Red;
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

public class MultipleSelectionToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] - количество выбранных точек в ViewModel 
        // values[1] - контекст данных текущего элемента (PlotPoint)
        // values[2] - коллекция выбранных элементов ListBox
        
        if (values.Length >= 3 && 
            values[0] is int selectedCount && 
            values[1] is PlotPoint currentItem && 
            values[2] is System.Collections.IList selectedItems)
        {
            // Показываем индикатор только если:
            // 1. Выбрано больше одной точки
            // 2. Текущая точка входит в выбранные
            bool isCurrentItemSelected = selectedItems.Contains(currentItem);
            return selectedCount > 1 && isCurrentItemSelected ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 