using System.Windows;
using System.Windows.Input;
using AT.App.ViewModels;
using AT.Automation.Desktop;
using AT.Automation.Web;
using AT.Core.Contracts;

namespace AT.App.Views;

/// <summary>
/// Nem-modális, sosem aktiválódó ablak (ShowActivated="False") — szándékosan NEM
/// ShowDialog()-gal nyitjuk meg, mert az elvenné a fókuszt a vizsgált alkalmazástól/felugró ablaktól.
/// </summary>
public partial class InspectorWindow : Window
{
    public InspectorWindow(
        DesktopAutomationDriver? desktopDriver,
        WebAutomationDriver? webDriver,
        InspectorPlatform initialPlatform,
        Action<LocatorType, string> onChosen)
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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}