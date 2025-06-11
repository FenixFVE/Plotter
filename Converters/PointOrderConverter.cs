using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WpfPlotApp.Models;

namespace WpfPlotApp.Converters
{
    public class PointOrderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && 
                values[0] is PlotPoint point && 
                values[1] is ObservableCollection<PlotPoint> points &&
                values[2] is int selectedCount)
            {
                // Если выбрано более одной точки, отключаем перемещение
                if (selectedCount > 1)
                {
                    return false;
                }
                
                var index = points.IndexOf(point);
                var direction = parameter?.ToString();
                
                return direction switch
                {
                    "Up" => index > 0,
                    "Down" => index >= 0 && index < points.Count - 1,
                    _ => false
                };
            }
            
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 