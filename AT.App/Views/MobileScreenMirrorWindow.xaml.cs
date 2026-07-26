using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AT.App.ViewModels;

namespace AT.App.Views;

// Önálló, mozgatható/dokkolható ablak az Élő kijelzőnek. A DataContext-je
// ugyanaz a MobileTestViewModel példány, mint a MobileTestView-é — nincs
// külön ViewModel, minden binding és parancs változatlanul működik.

public partial class MobileScreenMirrorWindow : Window
{
    // A gesztus-felismeréshez (koppintás / hosszan nyomás / húzás — lásd lent) a
    // lenyomás relatív (0..1) pozícióját és időpontját kell megjegyezni, hogy a
    // felengedéskor össze tudjuk vetni velük.
    private System.Windows.Point? _pointerDownPosition;
    private DateTime _pointerDownTime;

    public MobileScreenMirrorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Inspector (Elem-kiválasztó) módban egyetlen kattintás azonnal az Elem-kereső
    /// jelölt-listáját tölti fel, ahogy eddig is — ott nincs szükség húzás/hosszan
    /// nyomás felismerésre. Felvevő módban viszont csak MEGJEGYEZZÜK a lenyomás
    /// pozícióját/idejét — a tényleges döntés (koppintás/hosszan nyomás/húzás) a
    /// felengedéskor (ScreenImage_MouseLeftButtonUp) történik, az elmozdulás és az
    /// eltelt idő alapján. Az egeret Capture-özzük, hogy a felengedés akkor is
    /// megérkezzen, ha időközben gyors húzásnál kicsúszna a kép területéről.
    /// </summary>
    private void ScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel || (!viewModel.IsPicking && !viewModel.IsRecording))
            return;

        if (sender is not Image { Source: BitmapSource bitmap } image)
            return;

        if (!TryGetRelativePosition(image, bitmap, e, out var relativeX, out var relativeY))
            return;

        if (!viewModel.IsRecording)
        {
            // Inspector mód — nincs gesztus-felismerés, egyetlen kattintás azonnal
            // a jelölt-listát tölti fel, ahogy eddig is.
            _ = viewModel.CaptureElementAtAsync(relativeX, relativeY);
            return;
        }

        _pointerDownPosition = new System.Windows.Point(relativeX, relativeY);
        _pointerDownTime = DateTime.UtcNow;
        Mouse.Capture(image);
    }

    /// <summary>
    /// Felvevő módban itt dől el, hogy a lenyomás-felengedés páros koppintásnak,
    /// hosszan nyomásnak vagy húzásnak (Swipe) számít-e:
    /// - Ha az elmozdulás meghaladja a küszöböt (a kép szélességének/magasságának
    ///   ~8%-a) → Húzás, az elmozdulás fő iránya alapján (Fel/Le/Balra/Jobbra).
    /// - Különben, ha a lenyomva tartás ideje meghaladja az 500ms-t → Hosszan nyomás.
    /// - Egyébként → sima koppintás (Kattintás).
    /// </summary>
    private void ScreenImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel || _pointerDownPosition is null)
            return;

        if (sender is not Image { Source: BitmapSource bitmap } image)
        {
            _pointerDownPosition = null;
            return;
        }

        image.ReleaseMouseCapture();

        var down = _pointerDownPosition.Value;
        var downTime = _pointerDownTime;
        _pointerDownPosition = null;

        if (!TryGetRelativePosition(image, bitmap, e, out var relativeX, out var relativeY))
            return;

        const double swipeThreshold = 0.08;
        var dx = relativeX - down.X;
        var dy = relativeY - down.Y;

        if (Math.Abs(dx) > swipeThreshold || Math.Abs(dy) > swipeThreshold)
        {
            var direction = Math.Abs(dx) > Math.Abs(dy)
                ? (dx > 0 ? "Jobbra" : "Balra")
                : (dy > 0 ? "Le" : "Fel");

            viewModel.AddRecordedSwipeStep(direction);
            return;
        }

        var isLongPress = (DateTime.UtcNow - downTime).TotalMilliseconds > 500;
        _ = viewModel.CaptureElementAtAsync(down.X, down.Y, isLongPress);
    }

    /// <summary>Ugyanaz a letterbox-korrekciós logika, mint korábban is: a kép
    /// Stretch="Uniform", ezért a nyers kattintási koordinátát a ténylegesen kirajzolt
    /// kép-területhez kell igazítani, mielőtt relatív (0..1) koordinátává alakítjuk.</summary>
    private static bool TryGetRelativePosition(Image image, BitmapSource bitmap, MouseEventArgs e, out double relativeX, out double relativeY)
    {
        relativeX = 0;
        relativeY = 0;

        var containerWidth = image.ActualWidth;
        var containerHeight = image.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0)
            return false;

        var scale = Math.Min(containerWidth / bitmap.PixelWidth, containerHeight / bitmap.PixelHeight);
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        var pos = e.GetPosition(image);
        relativeX = (pos.X - offsetX) / renderedWidth;
        relativeY = (pos.Y - offsetY) / renderedHeight;

        return relativeX is >= 0 and <= 1 && relativeY is >= 0 and <= 1;
    }

    // A "Bezárás" gomb: leállítja a tükrözést, az Appium session-t és szervert
    // (StopAllCommand), majd bezárja az ablakot — a Close() a Window_Closing-on
    // keresztül fut le, ami a megszokott módon csak elrejti (Hide), nem szünteti
    // meg ténylegesen a példányt, hogy később újra elő lehessen hívni.

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MobileTestViewModel viewModel && viewModel.StopAllCommand.CanExecute(null))
            viewModel.StopAllCommand.Execute(null);

        Close();
    }


    // Közvetlen, determinisztikus jelzés arra, hogy a felhasználó bezárta (elrejtette)
    // az ablakot. A MobileMirrorWindowService erre iratkozik fel a WPF IsVisibleChanged
    // helyett, mert az utóbbi időzítése bizonyos Owner/Activate-kombinációk mellett
    // nem determinisztikus, és ismétlődő nyitás/zárás után előfordulhat, hogy elmarad
    // vagy duplán sül el.

    public event EventHandler? Hidden;


    // Az X gombbal történő bezárás nem szünteti meg az ablakot ténylegesen (Cancel=true,
    // Hide()), csak elrejti — így a MobileTestView-on lévő "Élő kijelző megnyitása" gombbal
    // újra elő lehet hívni ugyanazt a példányt, anélkül hogy újra kellene létrehozni.

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    // Ctrl+R — Felvétel indítása/leállítása. Ezt AZ ABLAKOT is ki kell egészíteni
    // ugyanezzel a paranccsal, mert amikor az Élő kijelző ablak van a képernyőn (és
    // Activate()-tel előtérbe kerül, lásd MobileMirrorWindowService), a billentyűzet-
    // fókusz ENNÉL az ablaknál van, nem a fő AT Studio ablaknál — a MobileTestView
    // PreviewKeyDown-ja emiatt soha nem kapná meg a lenyomott billentyűt.
    private void MobileScreenMirrorWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel)
            return;

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (ctrl && e.Key == Key.R)
        {
            viewModel.ToggleRecordingCommand.Execute(null);
            e.Handled = true;
        }
    }
}
