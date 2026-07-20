using System.Globalization;
using System.Windows.Data;

namespace AT.App.Converters;

/// <summary>"Felvétel indítása" / "Felvétel leállítása" — a IsRecording bool alapján,
/// ugyanabban a mintában, mint a MirrorButtonLabelConverter/PickingButtonLabelConverter.</summary>
public sealed class RecordingButtonLabelConverter : IValueConverter
{
    public static readonly RecordingButtonLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Felvétel leállítása" : "Felvétel indítása";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
