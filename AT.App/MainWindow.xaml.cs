using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AT.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AT.App;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AT.App.ViewModels.MainViewModel _mainViewModel;
    private bool _isReopening;

    // ===================== GLOBÁLIS Ctrl+R HOTKEY (Felvétel indítása/leállítása) =====================
    // A PreviewKeyDown-alapú, nézet-szintű Ctrl+R kezelés (lásd Web/Desktop/MobileTestView)
    // csak akkor sül el, ha az AT Studio ablaka van fókuszban — DE amíg a Felvevő fut, a
    // felhasználó a BÖNGÉSZŐBEN kattint/gépel, tehát a Chrome-ablak van fókuszban, nem az
    // AT Studio. Egy normál (nem-globális) billentyűparancs emiatt SOSEM jutna el hozzánk
    // ilyenkor — a Ctrl+R egyszerűen az oldal újratöltését váltaná ki a Chrome-ban.
    //
    // A Win32 RegisterHotKey ezt oldja meg: egy RENDSZERSZINTŰ gyorsbillentyűt regisztrál,
    // amit a Windows FÜGGETLENÜL attól, melyik ablak van épp fókuszban, mindig ehhez az
    // ablakhoz (MainWindow) továbbít egy WM_HOTKEY üzenetként. FONTOS MELLÉKHATÁS: amíg az
    // AT Studio fut, a Ctrl+R MINDEN MÁS alkalmazásban (pl. a Chrome-ban is) elveszti az
    // eredeti funkcióját (ott: oldal-újratöltés) — helyette mindig a Felvétel indítása/
    // leállítása fut le. Ha ez nem kívánt mellékhatás, szólj, és megoldjuk feltételesen
    // (pl. csak amíg ténylegesen fut egy felvétel).

    private const int HotkeyId = 0x4152; // tetszőleges, csak nekünk fenntartott azonosító
    private const uint ModControl = 0x0002;
    private const uint VkR = 0x52;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwndSource;

    public MainWindow(IThemeService themeService, IServiceProvider serviceProvider, AT.App.ViewModels.MainViewModel mainViewModel)
    {
        InitializeComponent();

        DataContext = mainViewModel;
        _mainViewModel = mainViewModel;

        _themeService = themeService;
        _serviceProvider = serviceProvider;
        _themeService.ThemeChanged += OnThemeChanged;

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(HwndHook);

        if (!RegisterHotKey(handle, HotkeyId, ModControl, VkR))
        {
            // Nem kritikus hiba — ha pl. egy másik program már lefoglalta ugyanezt a
            // kombinációt rendszerszinten, a nézet-szintű PreviewKeyDown-alapú Ctrl+R
            // (amíg az AT Studio van fókuszban) még mindig működik, csak a "böngésző
            // van fókuszban" eset nem lesz lefedve.
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyId);
        _hwndSource?.RemoveHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ExecuteGlobalToggleRecording();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>A jelenleg aktív nézet (Web/Desktop/Mobil) ToggleRecordingCommand-ját hívja
    /// meg — mindegyik ViewModel-en külön-külön létezik ugyanezen a néven, közös interfész
    /// nélkül, ezért egyszerű típus-eldöntéssel választjuk ki, melyiket kell hívni.</summary>
    private void ExecuteGlobalToggleRecording()
    {
        switch (_mainViewModel.CurrentViewModel)
        {
            case AT.App.ViewModels.WebTestViewModel web when web.ToggleRecordingCommand.CanExecute(null):
                web.ToggleRecordingCommand.Execute(null);
                break;

            case AT.App.ViewModels.DesktopTestViewModel desktop when desktop.ToggleRecordingCommand.CanExecute(null):
                desktop.ToggleRecordingCommand.Execute(null);
                break;

            case AT.App.ViewModels.MobileTestViewModel mobile when mobile.ToggleRecordingCommand.CanExecute(null):
                mobile.ToggleRecordingCommand.Execute(null);
                break;
        }
    }


    // A StaticResource binding WPF-ben egyszeri, betöltéskori feloldás — egy futásidejű
    // dictionary-csere (lásd ThemeService.ApplyTheme) NEM frissíti a már megjelenített
    // UI-elemeket. A garantáltan megbízható megoldás: teljesen újranyitjuk az ablakot,
    // hogy minden elem elsőként, a friss színekkel oldódjon fel. A MainViewModel Singleton,
    // tehát az állapot (aktuális nézet, stb.) megmarad az új ablakban is.

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


    // Mivel WindowStyle="None" (nincs natív címsor), a sidebar-ra kattintva-húzva
    // lehet mozgatni az ablakot. A navigációs gombok saját kattintás-kezelése
    // elsőbbséget élvez, ez csak az üres sidebar-területre kattintáskor sül el.

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