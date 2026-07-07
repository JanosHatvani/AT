using System.Windows;
using AT.App.Views;

namespace AT.App.Services;

/// <summary>
/// WPF-specifikus implementáció: egyetlen MobileScreenMirrorWindow példányt tart életben
/// (Show/Hide-dal, nem Close-dal), hogy a "Élő kijelző megnyitása" gomb mindig ugyanazt
/// az ablakot tudja visszahozni ahelyett, hogy újat hozna létre minden alkalommal.
/// Regisztráld singleton-ként a DI-konténerben:
///   services.AddSingleton&lt;IMobileMirrorWindowService, MobileMirrorWindowService&gt;();
/// </summary>
public sealed class MobileMirrorWindowService : IMobileMirrorWindowService
{
    private readonly IThemeService _themeService;
    private MobileScreenMirrorWindow? _window;
    private object? _lastDataContext;

    public bool IsOpen => _window is { IsVisible: true };

    public event EventHandler? Closed;

    public MobileMirrorWindowService(IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// A StaticResource binding WPF-ben egyszeri, betöltéskori feloldás — mivel ez az
    /// ablak Show/Hide-dal él (sosem záródik/nyílik újra magától, ellentétben a
    /// MainWindow-val), a témaváltás önmagában nem frissítené a színeit. Ha épp
    /// látható, becsukjuk és azonnal újranyitjuk egy friss példánnyal, ugyanazzal a
    /// DataContext-tel — ez garantálja, hogy a friss színekkel oldódjon fel minden elem.
    /// Ha épp rejtve van, nem kell semmit tenni: legközelebbi megnyitáskor úgyis új
    /// ablak-példány jön létre (lásd ShowOrActivate), ami már a friss témát kapja.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_window is not { IsVisible: true })
            return;

        var dataContext = _lastDataContext;

        _window.Hidden -= OnWindowHidden;
        _window.Close();
        _window = null;

        if (dataContext is not null)
            ShowOrActivate(dataContext);
    }

    public void ShowOrActivate(object dataContext)
    {
        _lastDataContext = dataContext;

        if (_window is null)
        {
            _window = new MobileScreenMirrorWindow();

            // Idempotens feliratkozás: -= majd += biztosítja, hogy akkor se
            // duplázódjon a handler, ha ez a blokk valamiért többször lefutna.
            _window.Hidden -= OnWindowHidden;
            _window.Hidden += OnWindowHidden;

            // A fő ablakhoz kötjük: a mirror-ablak együtt minimalizálódik/kerül
            // előtérbe a MainWindow-val, és nem marad árván, ha a fő ablak bezárul.
            if (Application.Current?.MainWindow is { } mainWindow && !ReferenceEquals(mainWindow, _window))
                _window.Owner = mainWindow;
        }

        // Minden megnyitáskor frissítjük a DataContext-et — új navigáció esetén
        // ez egy új MobileTestViewModel példány lesz (lásd NavigationService).
        _window.DataContext = dataContext;

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.Activate();
    }

    private void OnWindowHidden(object? sender, EventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    public void Hide()
    {
        if (_window is { IsVisible: true })
            _window.Hide();
    }
}
