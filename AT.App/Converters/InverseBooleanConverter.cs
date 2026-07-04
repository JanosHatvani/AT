using System.Globalization;
using System.Windows.Data;

namespace AT.App.Converters;

/// <summary>IsEnabled = !IsActive — az aktív menüpont gombja letiltva jelenik meg (accent színnel, ld. NavButtonStyle).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
