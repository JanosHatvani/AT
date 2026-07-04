using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AT.App.ViewModels;

namespace AT.App.Views;

public partial class MobileTestView : UserControl
{
    public MobileTestView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A kép Stretch="Uniform", ezért letterbox-sávok lehetnek — a nyers kattintási
    /// koordinátát a ténylegesen kirajzolt kép-területhez kell igazítani, mielőtt
    /// relatív (0..1) koordinátává alakítjuk.
    /// </summary>
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
}