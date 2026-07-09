using System.Globalization;
using System.Windows.Data;
using AT.Infrastructure;

namespace AT.App.Converters;

/// <summary>A ScheduleCadence enum-értékek magyar feliratra fordítása a ComboBox-okban — a projekt
/// meglévő *ToLabelConverter mintáját követi (pl. LocatorTypeToLabelConverter, MobileStepActionToLabelConverter).</summary>
public sealed class ScheduleCadenceToLabelConverter : IValueConverter
{
    public static readonly ScheduleCadenceToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ScheduleCadence.Hourly => "Óránként",
        ScheduleCadence.Daily => "Naponta",
        ScheduleCadence.Weekly => "Hetente",
        ScheduleCadence.Monthly => "Havonta",
        _ => value?.ToString() ?? ""
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
