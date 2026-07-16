namespace AT.App.Services;


// A világos/sötét téma futásidejű alkalmazását végzi: az Application.Resources
// MergedDictionaries-ben cseréli le a színpalettát tartalmazó resource dictionary-t.
// A Controls.xaml és minden View változtatás nélkül működik mindkét témával, mert
// mindig ugyanazokat a Brush.*/Color.* kulcsokat használják.

public interface IThemeService
{
    bool IsDarkTheme { get; }

    // Alkalmazza a megadott témát — ezt hívja meg az App induláskor és a Beállítások kapcsoló.
    void ApplyTheme(bool isDark);


    // A brush-ok Color-jának módosítása után váltódik ki — a StaticResource-szal már
    // feloldott UI-elemek nem kapnak automatikus render-értesítést egy Brush.Color
    // módosításról (ismert WPF-korlátozás), ezért a MainWindow erre az eseményre
    // feliratkozva explicit újraépíti a tartalmát, hogy a színek valóban látszódjanak.

    event EventHandler? ThemeChanged;
}
