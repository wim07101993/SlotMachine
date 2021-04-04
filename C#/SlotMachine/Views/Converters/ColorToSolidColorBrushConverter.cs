using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SlotMachine.Views.Converters
{
    public class ColorToSolidColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Color color 
                ? new SolidColorBrush(color)
                : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is SolidColorBrush brush
                ? brush.Color
                : default(Color);
        }
    }
}