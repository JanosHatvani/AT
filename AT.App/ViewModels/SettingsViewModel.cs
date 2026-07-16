using AT.App.Models;
using AT.App.Services;
using AT.Core.Models;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AT.App.ViewModels;


// A modulok alapértelmezéseit kezeli — ez váltja ki a régi kódban hardcode-olt
// útvonalakat (pl. az APK elérési útját), és helyben, a gépeden tárolja őket.

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IThemeService _themeService;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ITestCategoryService _categoryService;

    public string Title => "Beállítások";
    public string Description => "Alapértelmezett értékek a Web / Desktop / Mobil modulokhoz — a gépeden tárolva, nem kerülnek fel semmilyen szerverre.";

    public IReadOnlyList<string> AvailableBrowsers { get; } = new[] { "Chrome", "Firefox", "Edge" };


    // A sötét téma azonnal alkalmazódik (nem várja meg a Mentés gombot) és rögtön
    // el is mentődik — egy vizuális beállításnál ez jobb élmény, mint a "Mentés"-re
    // várakozás, hiszen a hatása amúgy is azonnal látszik.

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

    // Soha / csak hiba esetén / minden lépés után készítsen-e képernyőképet
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

    // A mappa-mező csak akkor szerkeszthető, ha egyáltalán készül képernyőkép.
    public bool IsFolderPathEditable => ScreenshotCaptureMode != ScreenshotCaptureMode.Never;

    partial void OnScreenshotCaptureModeChanged(ScreenshotCaptureMode value)
    {
        OnPropertyChanged(nameof(IsCaptureNever));
        OnPropertyChanged(nameof(IsCaptureOnErrorOnly));
        OnPropertyChanged(nameof(IsCaptureAlways));
        OnPropertyChanged(nameof(IsFolderPathEditable));
    }

    // Ha üres, a képernyőképek az Asztalra kerülnek.
    [ObservableProperty]
    private string? screenshotFolderPath;

    // Ha üres, a futtatási előzmények (riportok alapja) az Asztalra kerülnek
    [ObservableProperty]
    private string? testHistoryFolderPath;

    // ===================== RIPORT-EMAIL (ütemezett futtatás hibája esetén) =====================

    // Ha be van kapcsolva, egy ütemezett (automatikus) futtatás hibája esetén
    // riport-emailt küld. Kézi futtatás hibája esetén nem küld.
    [ObservableProperty]
    private bool emailNotificationsEnabled;

    // Csak akkor szerkeszthetők az SMTP-mezők, ha az email-küldés be van kapcsolva —
    // enélkül a kikapcsolt állapotban is aktívnak tűnnének a mezők, ami félrevezető lenne.
    public bool AreEmailFieldsEditable => EmailNotificationsEnabled;

    partial void OnEmailNotificationsEnabledChanged(bool value) => OnPropertyChanged(nameof(AreEmailFieldsEditable));

    [ObservableProperty]
    private string? smtpHost;

    [ObservableProperty]
    private int smtpPort = 587;

    [ObservableProperty]
    private string? smtpUsername;

    [ObservableProperty]
    private string? smtpPassword;

    [ObservableProperty]
    private bool smtpUseSsl = true;

    [ObservableProperty]
    private string? emailFrom;

    // Vesszővel/pontosvesszővel/soronként elválasztott címzett-lista, pl. "a@x.hu, b@y.hu".
    [ObservableProperty]
    private string? emailRecipients;

    // ===================== TESZT-KATEGÓRIÁK =====================

    // A meglévő kategóriák listája — mindegyik sor saját platform-checkboxokkal
    // (Web/Desktop/Mobil), amiket közvetlenül szerkesztve azonnal menti a változást.
    public ObservableCollection<CategoryRow> Categories { get; } = new();

    public bool HasCategories => Categories.Count > 0;

    [ObservableProperty]
    private string newCategoryName = "";

    [ObservableProperty]
    private bool newCategoryIsWeb;

    [ObservableProperty]
    private bool newCategoryIsDesktop;

    [ObservableProperty]
    private bool newCategoryIsMobile;

    public SettingsViewModel(
        ISettingsService settingsService,
        INotificationService notificationService,
        IThemeService themeService,
        IEmailNotificationService emailNotificationService,
        ITestCategoryService categoryService)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _themeService = themeService;
        _emailNotificationService = emailNotificationService;
        _categoryService = categoryService;
        LoadFromSettings();
        LoadCategories();
    }

    private void LoadCategories()
    {
        Categories.Clear();
        foreach (var category in _categoryService.Categories.OrderBy(c => c.Name))
            Categories.Add(new CategoryRow(category, this));

        OnPropertyChanged(nameof(HasCategories));
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            _notificationService.Show("Add meg a kategória nevét.", NotificationType.Warning);
            return;
        }

        if (_categoryService.Categories.Any(c => string.Equals(c.Name, NewCategoryName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _notificationService.Show("Már létezik ilyen nevű kategória.", NotificationType.Warning);
            return;
        }

        var targets = new List<AutomationTarget>();
        if (NewCategoryIsWeb) targets.Add(AutomationTarget.Web);
        if (NewCategoryIsDesktop) targets.Add(AutomationTarget.Desktop);
        if (NewCategoryIsMobile) targets.Add(AutomationTarget.Android);

        if (targets.Count == 0)
        {
            _notificationService.Show("Válassz ki legalább egy platformot a kategóriához.", NotificationType.Warning);
            return;
        }

        var category = new TestCategory { Name = NewCategoryName.Trim(), AllowedTargets = targets };
        await _categoryService.AddAsync(category);
        LoadCategories();

        NewCategoryName = "";
        NewCategoryIsWeb = false;
        NewCategoryIsDesktop = false;
        NewCategoryIsMobile = false;

        _notificationService.Show("Kategória létrehozva.", NotificationType.Success);
    }

    // A CategoryRow hívja, amikor egy platform-checkbox vagy a név megváltozik —
    // azonnal menti a változást, nincs külön "Mentés" gomb soronként.
    internal async Task UpdateCategoryAsync(TestCategory category)
    {
        await _categoryService.UpdateAsync(category);
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CategoryRow? row)
    {
        if (row is null)
            return;

        var confirmed = AT.App.Views.ConfirmDialog.Show(
            System.Windows.Application.Current.MainWindow,
            "Kategória törlése",
            $"Biztosan törlöd a(z) \"{row.Name}\" kategóriát? A meglévő tesztek/ütemezések, amik ezt a kategóriát használták, megtartják a hivatkozást, de az a szűrőkben \"Kategória nélkül\"-ként fog megjelenni.",
            confirmButtonText: "Törlés",
            isDestructive: true);

        if (!confirmed)
            return;

        await _categoryService.DeleteAsync(row.Id);
        Categories.Remove(row);
        OnPropertyChanged(nameof(HasCategories));

        _notificationService.Show("Kategória törölve.", NotificationType.Info);
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

        EmailNotificationsEnabled = s.EmailNotificationsEnabled;
        SmtpHost = s.SmtpHost;
        SmtpPort = s.SmtpPort;
        SmtpUsername = s.SmtpUsername;
        SmtpPassword = s.SmtpPassword;
        SmtpUseSsl = s.SmtpUseSsl;
        EmailFrom = s.EmailFrom;
        EmailRecipients = s.EmailRecipients;

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

        s.EmailNotificationsEnabled = EmailNotificationsEnabled;
        s.SmtpHost = NullIfEmpty(SmtpHost);
        s.SmtpPort = SmtpPort <= 0 ? 587 : SmtpPort;
        s.SmtpUsername = NullIfEmpty(SmtpUsername);
        s.SmtpPassword = NullIfEmpty(SmtpPassword);
        s.SmtpUseSsl = SmtpUseSsl;
        s.EmailFrom = NullIfEmpty(EmailFrom);
        s.EmailRecipients = NullIfEmpty(EmailRecipients);

        await _settingsService.SaveAsync();
        _notificationService.Show("Beállítások mentve.", NotificationType.Success);
    }


    // A jelenleg beírt (de esetleg még nem mentett) SMTP-adatokkal próbál küldeni egy teszt
    // emailt — előbb ideiglenesen elmenti a mezőket a Settings-be, hogy az
    // EmailNotificationService (ami a Settings.Current-ből olvas) a friss értékeket lássa,
    // majd visszaküldi az eredményt toast-ként. Ha a küldés sikeres volt, a beállítások
    // (amiket úgyis menteni kellett a küldéshez) a lemezen is maradnak.

    [RelayCommand]
    private async Task SendTestEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost) || string.IsNullOrWhiteSpace(EmailFrom) || string.IsNullOrWhiteSpace(EmailRecipients))
        {
            _notificationService.Show("Add meg az SMTP host, feladó és címzett mezőket a teszt email küldéséhez.", NotificationType.Warning);
            return;
        }

        // Elmentjük a jelenlegi mezőket, hogy az EmailNotificationService a beírt (nem  feltétlenül még "Mentés"-sel megerősített) adatokkal próbálkozzon.

        await SaveAsync();

        var success = await _emailNotificationService.SendTestEmailAsync();
        if (success)
            _notificationService.Show("Teszt email elküldve — ellenőrizd a postaládát.", NotificationType.Success);

        // Sikertelen esetben az EmailNotificationService már megjelenítette a részletes hiba-toastot.
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

        EmailNotificationsEnabled = false;
        SmtpHost = null;
        SmtpPort = 587;
        SmtpUsername = null;
        SmtpPassword = null;
        SmtpUseSsl = true;
        EmailFrom = null;
        EmailRecipients = null;

        _notificationService.Show("Alapértelmezett értékek visszaállítva — a Mentés gombbal rögzítheted.", NotificationType.Info);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}


// UI-oldali wrapper egy TestCategory köré — a platform-checkboxok (Web/Desktop/Mobil)
// és a név közvetlen szerkesztését teszi lehetővé a listában, azonnali mentéssel (nincs
// külön "Mentés" gomb soronként, mint a fő Beállítások szekciónál). Legalább egy platform
// kiválasztva kell maradjon — ha az utolsót is levennék, a checkbox visszaáll.

public sealed partial class CategoryRow : ObservableObject
{
    private readonly TestCategory _category;
    private readonly SettingsViewModel _owner;

    public string Id => _category.Id;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool isWeb;

    [ObservableProperty]
    private bool isDesktop;

    [ObservableProperty]
    private bool isMobile;

    public CategoryRow(TestCategory category, SettingsViewModel owner)
    {
        _category = category;
        _owner = owner;

        name = category.Name;
        isWeb = category.AllowedTargets.Contains(AutomationTarget.Web);
        isDesktop = category.AllowedTargets.Contains(AutomationTarget.Desktop);
        isMobile = category.AllowedTargets.Contains(AutomationTarget.Android);
    }

    partial void OnNameChanged(string value) => _ = PersistAsync();
    partial void OnIsWebChanged(bool value) => _ = PersistWithGuardAsync(AutomationTarget.Web, value);
    partial void OnIsDesktopChanged(bool value) => _ = PersistWithGuardAsync(AutomationTarget.Desktop, value);
    partial void OnIsMobileChanged(bool value) => _ = PersistWithGuardAsync(AutomationTarget.Android, value);

    /// <summary>Ha a felhasználó az utolsó bejelölt platformot is levenné, visszaállítja
    /// azt (legalább egy platform mindig kell maradjon), és figyelmeztet.</summary>
    private async Task PersistWithGuardAsync(AutomationTarget target, bool newValue)
    {
        var wouldHaveAnyTarget = IsWeb || IsDesktop || IsMobile;
        if (!newValue && !wouldHaveAnyTarget)
        {
            // Ez az állapot akkor állna elő, ha az utolsó platformot is kikapcsolnák —
            // visszaállítjuk, hogy legalább egy mindig aktív maradjon.
            switch (target)
            {
                case AutomationTarget.Web: IsWeb = true; break;
                case AutomationTarget.Desktop: IsDesktop = true; break;
                case AutomationTarget.Android: IsMobile = true; break;
            }
            return;
        }

        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        _category.Name = Name;

        var targets = new List<AutomationTarget>();
        if (IsWeb) targets.Add(AutomationTarget.Web);
        if (IsDesktop) targets.Add(AutomationTarget.Desktop);
        if (IsMobile) targets.Add(AutomationTarget.Android);
        _category.AllowedTargets = targets;

        await _owner.UpdateCategoryAsync(_category);
    }
}
