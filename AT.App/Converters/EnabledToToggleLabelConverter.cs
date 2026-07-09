using System.Globalization;
using System.Windows.Data;

namespace AT.App.Converters;

/// <summary>A ScheduledTasksView kártyáján lévő be/kikapcsoló gomb feliratát adja vissza:
/// ha a feladat épp be van kapcsolva, "Kikapcsolás"-t mutat (mert erre kattintva kikapcsolódna), és fordítva.</summary>
public sealed class EnabledToToggleLabelConverter : IValueConverter
{
    public static readonly EnabledToToggleLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Kikapcsolás" : "Bekapcsolás";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
