using AT.App.Models;
using AT.App.Services;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;


public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IThemeService _themeService;

    public string Title => "Beállítások";
    public string Description => "Alapértelmezett értékek a Web / Desktop / Mobil modulokhoz — a gépeden tárolva";

    public IReadOnlyList<string> AvailableBrowsers { get; } = new[] { "Chrome", "Firefox", "Edge" };


    [ObservableProperty]
    private bool isDarkTheme;

    partial void OnIsDarkThemeChanged(bool value)
    {
        _themeService.ApplyTheme(value);
        _settingsService.Current.IsDarkTheme = value;
        _ = _settingsService.SaveAsync();
    }

    [ObservableProperty]
    private string? androidSdkRoot;

    [ObservableProperty]
    private string defaultBrowser = "Chrome";

    [ObservableProperty]
    private int defaultTimeoutSeconds = 10;

    [ObservableProperty]
    private string? defaultAvdName;

    [ObservableProperty]
    private string? defaultApkPath;

    [ObservableProperty]
    private string? defaultDesktopAppPath;

    /// <summary>Soha / csak hiba esetén / minden lépés után készítsen-e képernyőképet.</summary>
    [ObservableProperty]
    private ScreenshotCaptureMode screenshotCaptureMode = ScreenshotCaptureMode.Never;

    public bool IsCaptureNever
    {
        get => ScreenshotCaptureMode == ScreenshotCaptureMode.Never;
        set { if (value) ScreenshotCaptureMode = ScreenshotCaptureMode.Never; }
    }

    public bool IsCaptureOnErrorOnly
    {
        get => ScreenshotCaptureMode == ScreenshotCaptureMode.OnErrorOnly;
        set { if (value) ScreenshotCaptureMode = ScreenshotCaptureMode.OnErrorOnly; }
    }

    public bool IsCaptureAlways
    {
        get => ScreenshotCaptureMode == ScreenshotCaptureMode.Always;
        set { if (value) ScreenshotCaptureMode = ScreenshotCaptureMode.Always; }
    }

    /// <summary>A mappa-mező csak akkor szerkeszthető, ha egyáltalán készül képernyőkép.</summary>
    public bool IsFolderPathEditable => ScreenshotCaptureMode != ScreenshotCaptureMode.Never;

    partial void OnScreenshotCaptureModeChanged(ScreenshotCaptureMode value)
    {
        OnPropertyChanged(nameof(IsCaptureNever));
        OnPropertyChanged(nameof(IsCaptureOnErrorOnly));
        OnPropertyChanged(nameof(IsCaptureAlways));
        OnPropertyChanged(nameof(IsFolderPathEditable));
    }

    /// <summary>Ha üres, a képernyőképek az Asztalra kerülnek.</summary>
    [ObservableProperty]
    private string? screenshotFolderPath;

    /// <summary>Ha üres, a futtatási előzmények (riportok alapja) az Asztalra kerülnek.</summary>
    [ObservableProperty]
    private string? testHistoryFolderPath;

    public SettingsViewModel(ISettingsService settingsService, INotificationService notificationService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _themeService = themeService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;
        AndroidSdkRoot = s.AndroidSdkRoot;
        DefaultBrowser = s.DefaultBrowser;
        DefaultTimeoutSeconds = s.DefaultTimeoutSeconds;
        DefaultAvdName = s.DefaultAvdName;
        DefaultApkPath = s.DefaultApkPath;
        DefaultDesktopAppPath = s.DefaultDesktopAppPath;
        ScreenshotCaptureMode = s.ScreenshotCaptureMode;
        ScreenshotFolderPath = s.ScreenshotFolderPath;
        TestHistoryFolderPath = s.TestHistoryFolderPath;

        // Az IsDarkTheme betöltése NEM az [ObservableProperty] setter-en keresztül történik itt,
        // mert az OnIsDarkThemeChanged újra elmentené a beállítást és újra alkalmazná a témát —
        // ez az app induláskor már megtörtént (lásd App.xaml.cs OnStartup), itt csak a UI-t
        // szinkronizáljuk a tényleges állapottal, mentés/alkalmazás kiváltása nélkül.
        isDarkTheme = s.IsDarkTheme;
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    [RelayCommand]
    private void BrowseAndroidSdkRoot()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Android SDK gyökérmappa kiválasztása" };
        if (dialog.ShowDialog() == true)
            AndroidSdkRoot = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseApkPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Alapértelmezett APK kiválasztása",
            Filter = "Android csomag (*.apk)|*.apk|Minden fájl (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            DefaultApkPath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseDesktopAppPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Alapértelmezett alkalmazás kiválasztása",
            Filter = "Futtatható fájl (*.exe)|*.exe|Minden fájl (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            DefaultDesktopAppPath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseScreenshotFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Képernyőkép-mentési mappa kiválasztása" };
        if (dialog.ShowDialog() == true)
            ScreenshotFolderPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseTestHistoryFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Előzmények mentési mappájának kiválasztása" };
        if (dialog.ShowDialog() == true)
            TestHistoryFolderPath = dialog.FolderName;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.Current;
        s.AndroidSdkRoot = NullIfEmpty(AndroidSdkRoot);
        s.DefaultBrowser = DefaultBrowser;
        s.DefaultTimeoutSeconds = Math.Max(1, DefaultTimeoutSeconds);
        s.DefaultAvdName = NullIfEmpty(DefaultAvdName);
        s.DefaultApkPath = NullIfEmpty(DefaultApkPath);
        s.DefaultDesktopAppPath = NullIfEmpty(DefaultDesktopAppPath);
        s.ScreenshotCaptureMode = ScreenshotCaptureMode;
        s.ScreenshotFolderPath = NullIfEmpty(ScreenshotFolderPath);
        s.TestHistoryFolderPath = NullIfEmpty(TestHistoryFolderPath);

        await _settingsService.SaveAsync();
        _notificationService.Show("Beállítások mentve.", NotificationType.Success);
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        AndroidSdkRoot = null;
        DefaultBrowser = "Chrome";
        DefaultTimeoutSeconds = 10;
        DefaultAvdName = null;
        DefaultApkPath = null;
        DefaultDesktopAppPath = null;
        ScreenshotCaptureMode = ScreenshotCaptureMode.Never;
        ScreenshotFolderPath = null;
        TestHistoryFolderPath = null;
        IsDarkTheme = false;
        _notificationService.Show("Alapértelmezett értékek visszaállítva — a Mentés gombbal rögzítheted.", NotificationType.Info);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
