using System.Windows;
using System.Windows.Input;
using AT.App.ViewModels;

namespace AT.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Natív fejléc nincs (WindowStyle="None") — a logó-sáv húzásával mozgatható az ablak.</summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
