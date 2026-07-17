using System.Windows;
using System.Windows.Input;
using AT.App.ViewModels;
using AT.Automation.Desktop;
using AT.Automation.Web;
using AT.Core.Contracts;

namespace AT.App.Views;


// Nem-modális, sosem aktiválódó ablak (ShowActivated="False") — szándékosan NEM
// ShowDialog()-gal nyitjuk meg, mert az elvenné a fókuszt a vizsgált alkalmazástól/felugró ablaktól.

public partial class InspectorWindow : Window
{
    public InspectorWindow(
        DesktopAutomationDriver? desktopDriver,
        WebAutomationDriver? webDriver,
        InspectorPlatform initialPlatform,
        Action<LocatorType, string, int?> onChosen)
    {
        InitializeComponent();
        DataContext = new InspectorWindowViewModel(desktopDriver, webDriver, initialPlatform, onChosen);

        Loaded += (_, _) => PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Bottom - ActualHeight - 24;
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InspectorWindowViewModel viewModel)
            await viewModel.HandleWindowClosingAsync();

        Close();
    }

    /// <summary>A címsor natív ✕ gombja (vagy Alt+F4) is a Window Closing eseményén
    /// keresztül fut, nem a CloseButton_Click-en — enélkül csak a "Bezárás" gomb zárná
    /// le rendesen a böngésző-session-t, az X gomb "csendben" hagyná futni a háttérben.</summary>
    private async void InspectorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is InspectorWindowViewModel viewModel)
            await viewModel.HandleWindowClosingAsync();
    }
}