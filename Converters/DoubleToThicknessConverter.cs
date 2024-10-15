using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CallRecording.Converters
{
    public class DoubleToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // 将 double 值转换为 Thickness，通常用于设置 Margin 或 Padding
                return new Thickness(doubleValue, 0, 0, 0);  // 仅设置左边距
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
