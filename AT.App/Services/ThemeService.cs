using System.Windows;

namespace AT.App.Services;


// A világos/sötét téma alkalmazása: lecseréli a színpalettát tartalmazó resource
// dictionary-t (Colors.xaml / Colors.Dark.xaml) az Application.Resources
// MergedDictionaries[0] pozíciójában.

// FONTOS: mivel a StaticResource binding WPF-ben egyszeri, betöltéskori feloldás,
// ez a dictionary-csere ÖNMAGÁBAN nem frissítené a már megjelenített UI-t — ezért
// a ThemeChanged eseményre feliratkozva a MainWindow teljesen újranyitja saját magát
// (lásd MainWindow.RequestReopen), hogy minden elem elsőként, a friss dictionary-ből
// oldódjon fel. Ez egy garantáltan megbízható, egyszerű megoldás.

public sealed class ThemeService : IThemeService
{
    private const string LightThemeUri = "Themes/Colors.xaml";
    private const string DarkThemeUri = "Themes/Colors.Dark.xaml";
    private const int ColorsDictionaryIndex = 0;

    public bool IsDarkTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public void ApplyTheme(bool isDark)
    {
        IsDarkTheme = isDark;

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
        var newUri = isDark ? DarkThemeUri : LightThemeUri;

        var newDictionary = new ResourceDictionary { Source = new Uri(newUri, UriKind.Relative) };

        if (mergedDictionaries.Count > ColorsDictionaryIndex)
            mergedDictionaries[ColorsDictionaryIndex] = newDictionary;
        else
            mergedDictionaries.Insert(ColorsDictionaryIndex, newDictionary);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
