using System;
using System.Globalization;
using System.Windows.Data;
using ScottPlot;

namespace WpfPlotApp.Converters;

public class MarkerShapeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MarkerShape shape)
        {
            return shape.ToString();
        }
        return "FilledCircle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string shapeName)
        {
            if (Enum.TryParse<MarkerShape>(shapeName, out var shape))
                return shape;
        }
        return MarkerShape.FilledCircle;
    }
} 