using System.Windows;
using System.Windows.Input;
using AT.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AT.App;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly IServiceProvider _serviceProvider;
    private bool _isReopening;

    public MainWindow(IThemeService themeService, IServiceProvider serviceProvider, AT.App.ViewModels.MainViewModel mainViewModel)
    {
        InitializeComponent();

        DataContext = mainViewModel;

        _themeService = themeService;
        _serviceProvider = serviceProvider;
        _themeService.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// A StaticResource binding WPF-ben egyszeri, betöltéskori feloldás — egy futásidejű
    /// dictionary-csere (lásd ThemeService.ApplyTheme) NEM frissíti a már megjelenített
    /// UI-elemeket. A garantáltan megbízható megoldás: teljesen újranyitjuk az ablakot,
    /// hogy minden elem elsőként, a friss színekkel oldódjon fel. A MainViewModel Singleton,
    /// tehát az állapot (aktuális nézet, stb.) megmarad az új ablakban is.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_isReopening)
            return;

        _isReopening = true;

        // Leiratkozunk, mielőtt bezárnánk magunkat — a régi ablak-példány innentől
        // nem figyel több témaváltásra, azt majd az új példány veszi át.
        _themeService.ThemeChanged -= OnThemeChanged;

        var newWindow = _serviceProvider.GetRequiredService<MainWindow>();
        Application.Current.MainWindow = newWindow;
        newWindow.Show();

        Close();
    }

    /// <summary>
    /// Mivel WindowStyle="None" (nincs natív címsor), a sidebar-ra kattintva-húzva
    /// lehet mozgatni az ablakot. A navigációs gombok saját kattintás-kezelése
    /// elsőbbséget élvez, ez csak az üres sidebar-területre kattintáskor sül el.
    /// </summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
