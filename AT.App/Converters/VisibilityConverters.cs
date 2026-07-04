using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AT.App.Converters;

/// <summary>Visible, ha a bemenet nem null/üres string (pl. hibaüzenet megjelenítése).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s
            ? (string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible)
            : (value is null ? Visibility.Collapsed : Visibility.Visible);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible, ha a bemenet (int) nulla — "üres lista" placeholder szöveghez.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public static readonly ZeroToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true → Visible, false → Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>false → Visible, true → Collapsed (a BoolToVisibilityConverter ellentéte).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>A kijelző-tükrözés gomb feliratát váltja a mirroring állapota szerint.</summary>
public sealed class MirrorButtonLabelConverter : IValueConverter
{
    public static readonly MirrorButtonLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Leállítás" : "Tükrözés indítása";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Az elem-kiválasztás gomb feliratát váltja.</summary>
public sealed class PickingButtonLabelConverter : IValueConverter
{
    public static readonly PickingButtonLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Mégse" : " Elem kiválasztás";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Kereszt-kurzor elem-kiválasztás módban, egyébként alapértelmezett nyíl.</summary>
public sealed class PickingCursorConverter : IValueConverter
{
    public static readonly PickingCursorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? System.Windows.Input.Cursors.Cross : System.Windows.Input.Cursors.Arrow;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}