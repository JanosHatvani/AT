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
    public MobileScreenMirrorWindow()
    {
        InitializeComponent();
    }

    
    // Ugyanaz a letterbox-korrekciós logika, mint a MobileTestView-ban:
    // a kép Stretch="Uniform", ezért a nyers kattintási koordinátát a
    // ténylegesen kirajzolt kép-területhez kell igazítani.
    
    private void ScreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MobileTestViewModel viewModel || !viewModel.IsPicking)
            return;

        if (sender is not Image { Source: BitmapSource bitmap } image)
            return;

        var containerWidth = image.ActualWidth;
        var containerHeight = image.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0)
            return;

        var scale = Math.Min(containerWidth / bitmap.PixelWidth, containerHeight / bitmap.PixelHeight);
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        var pos = e.GetPosition(image);
        var relativeX = (pos.X - offsetX) / renderedWidth;
        var relativeY = (pos.Y - offsetY) / renderedHeight;

        if (relativeX is < 0 or > 1 || relativeY is < 0 or > 1)
            return;

        _ = viewModel.CaptureElementAtAsync(relativeX, relativeY);
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
}
