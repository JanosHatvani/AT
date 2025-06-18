using System;
using System.Globalization;
using System.Windows.Data;

namespace MainWindow
{
    public class DurationExceedsTimeoutConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is double duration && values[1] is int timeout)
            {
                return duration > timeout;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
