using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WpfPlotApp.Models;

namespace WpfPlotApp.Converters
{
    public class FunctionOrderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && 
                values[0] is PlotFunction function && 
                values[1] is ObservableCollection<PlotFunction> functions &&
                values[2] is int selectedCount)
            {
                // Если выбрано более одной функции, отключаем перемещение
                if (selectedCount > 1)
                {
                    return false;
                }
                
                var index = functions.IndexOf(function);
                var direction = parameter?.ToString();
                
                return direction switch
                {
                    "Up" => index > 0,
                    "Down" => index >= 0 && index < functions.Count - 1,
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