using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AT.App.Converters;

/// <summary>
/// A lépéslista sorainak kijelölés-kiemeléséhez: összehasonlítja a sor saját
/// TestStepRow-ját a ViewModel SelectedStep property-jével (referencia szerint),
/// és ha egyeznek, egy halvány accent-hátteret ad vissza — ellenkező esetben átlátszót.
/// </summary>
public sealed class SameReferenceToBrushConverter : IMultiValueConverter
{
    public static readonly SameReferenceToBrushConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is null || values[1] is null)
            return Brushes.Transparent;

        var isSelected = ReferenceEquals(values[0], values[1]);
        if (!isSelected)
            return Brushes.Transparent;

        return Application.Current.TryFindResource("Brush.AccentMuted") as Brush ?? Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
