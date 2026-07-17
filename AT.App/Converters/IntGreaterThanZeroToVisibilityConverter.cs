using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AT.App.Converters;

/// <summary>
/// Visible-t ad vissza, ha a bekötött int érték nagyobb, mint 0 — egyébként Collapsed.
/// A lépéslista "Retry ×N" jelvényéhez kell: a TestStep.RetryCount NEM nullable int
/// (alapértelmezetten 0, ha nincs retry beállítva), ezért a NullToVisibilityConverter
/// (ami a nullable mezőknél, pl. ElementIndex-nél működik) itt nem használható —
/// egy 0 érték sosem "null", de a jelvénynek 0-nál el kell tűnnie.
/// </summary>
public sealed class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public static readonly IntGreaterThanZeroToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int intValue && intValue > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
