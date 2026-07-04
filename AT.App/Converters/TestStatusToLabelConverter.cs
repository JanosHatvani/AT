using System.Globalization;
using System.Windows.Data;
using AT.Core.Models;

namespace AT.App.Converters;

public sealed class TestStatusToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            TestStatus.NotRun => "Nincs futtatva",
            TestStatus.Running => "Fut…",
            TestStatus.Passed => "Sikeres",
            TestStatus.Failed => "Sikertelen",
            TestStatus.Skipped => "Kihagyva",
            _ => string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
