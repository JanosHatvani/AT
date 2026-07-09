using System.Globalization;
using System.Windows.Data;
using AT.App.ViewModels;

namespace AT.App.Converters;

/// <summary>A PlatformFilter enum-értékek magyar feliratra fordítása a szűrő ComboBox-ban.</summary>
public sealed class PlatformFilterToLabelConverter : IValueConverter
{
    public static readonly PlatformFilterToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        PlatformFilter.All => "Mind",
        PlatformFilter.Web => "Web",
        PlatformFilter.Desktop => "Windows desktop",
        PlatformFilter.Mobile => "Mobil (Android)",
        _ => value?.ToString() ?? ""
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
