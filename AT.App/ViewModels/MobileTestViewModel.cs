using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Mobile;
using AT.Core.Contracts;
using AT.Core.Models;
using AT.Infrastructure;
using AT.App.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class MobileTestViewModel : ObservableObject, INavigationAware
{
    private readonly MobileAutomationDriver _driver;
    private readonly INotificationService _notificationService;
    private readonly AT.Infrastructure.ITestSuiteFileService _fileService;
    private readonly AT.Infrastructure.ISettingsService _settingsService;
    private readonly IMobileMirrorWindowService _mirrorWindowService;
    private readonly ITestRunHistoryService _historyService;
    private readonly ITestReportService _reportService;
    private readonly DispatcherTimer _mirrorTimer;
    private bool _isRefreshingDeviceStatus;
    private readonly DispatcherTimer _deviceStatusTimer;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ISchedulerService _schedulerService;
    private readonly ITestCategoryService _categoryService;
    private readonly IAndroidSdkInstallerService _androidSdkInstallerService;

    /// <summary>
    /// Igaz, ha nincs telepített/beállított Android SDK — a nézet ekkor egy
    /// figyelmeztető sávot mutat, és a Futtatás/Mentés/Ütemezés gombok inaktívak
    /// maradnak (lásd CanRun()). A MainViewModel navigáláskor (RefreshAndroidSdkStatusCommand)
    /// és a felvett SDK-telepítő dialógus lezárása után is frissíti ezt az állapotot.
    /// </summary>
    [ObservableProperty]
    private bool isAndroidSdkMissing;

    partial void OnIsAndroidSdkMissingChanged(bool value) => RunStepsCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void RefreshAndroidSdkStatus()
    {
        IsAndroidSdkMissing = !_androidSdkInstallerService.IsInstalled();
    }

    /// <summary>A figyelmeztető sáv "Telepítés most" gombja — ugyanazt a dialógust nyitja meg,
    /// mint a navigáció-elfogás (MainViewModel), hogy a nézeten belül maradva is pótolható
    /// legyen a hiányzó SDK, nem csak a menüpontra való (újra-)kattintással.</summary>
    [RelayCommand]
    private void OpenAndroidSdkSetup()
    {
        AT.App.Views.AndroidSdkSetupWindow.Show(Application.Current.MainWindow, _androidSdkInstallerService);
        RefreshAndroidSdkStatus();
    }

    /// <summary>A folyamatban lévő (vagy legutóbb befejezett) futtatás képernyőkép-mappája — null, ha ehhez a futtatáshoz nem készül kép.</summary>
    private string? _currentRunScreenshotFolder;

    /// <summary>A legutóbbi futtatás összegzése — a "Riport exportálása" gomb ezt írja ki HTML-be.</summary>
    private TestRunRecord? _lastRunRecord;

    public bool HasLastRun => _lastRunRecord is not null;

    // ===================== ESZKÖZ-ÁLLAPOT (Csatlakoztatva: <telefon neve> sáv) =====================

    /// <summary>Igaz, ha az utolsó ellenőrzéskor volt egy használható (nem unauthorized/offline)
    /// ADB-eszköz csatlakoztatva. A nézet ez alapján dönt zöld/piros jelző-pötty között.</summary>
    [ObservableProperty]
    private bool isDeviceConnected;

    /// <summary>Emberi olvasásra szánt állapot-szöveg, pl. "Csatlakoztatva: SM-S911B (R58N70ABCDE)",
    /// "Az eszköz engedélyre vár a telefonon" vagy "Nincs csatlakoztatott eszköz".</summary>
    [ObservableProperty]
    private string deviceStatusText = "Eszközállapot ellenőrzése…";

    /// <summary>
    /// Újra lekérdezi az ADB-n keresztül csatlakoztatott eszköz állapotát — a nézet
    /// betöltésekor (konstruktor), a "Frissítés" gomb kattintására, VALAMINT a
    /// _deviceStatusTimer-en keresztül automatikusan, 3 másodpercenként. A
    /// _isRefreshingDeviceStatus reentrancy-védelem biztosítja, hogy egyszerre
    /// legfeljebb egy lekérdezés (adb-folyamat) fusson — ha egy korábbi hívás még
    /// nem fejeződött be, a következő tick/kattintás egyszerűen kimarad.
    /// </summary>
    [RelayCommand]
    private async Task RefreshDeviceStatusAsync()
    {
        if (_isRefreshingDeviceStatus)
            return;

        _isRefreshingDeviceStatus = true;
        try
        {
            DeviceStatusText = "Eszközállapot ellenőrzése…";

            MobileDeviceInfo info;
            try
            {
                info = await _driver.GetConnectedDeviceInfoAsync();
            }
            catch (Exception ex)
            {
                IsDeviceConnected = false;
                DeviceStatusText = $"Eszközállapot lekérdezése sikertelen: {ex.Message}";
                return;
            }

            if (info.IsConnected)
            {
                IsDeviceConnected = true;
                DeviceStatusText = string.IsNullOrWhiteSpace(info.DeviceModel)
                    ? $"Csatlakoztatva ({info.SerialNumber})"
                    : $"Csatlakoztatva: {info.DeviceModel} ({info.SerialNumber})";
            }
            else if (info.IsUnauthorizedOrOffline)
            {
                IsDeviceConnected = false;
                DeviceStatusText = $"Az eszköz ({info.SerialNumber}) csatlakoztatva van, de engedélyre vár — " +
                                    "fogadd el az USB-hibakeresési engedélykérést a telefon képernyőjén.";
            }
            else
            {
                IsDeviceConnected = false;
                DeviceStatusText = "Nincs csatlakoztatott eszköz — csatlakoztass egy telefont USB-n, és engedélyezd rajta az USB-hibakeresést.";
            }
        }
        finally
        {
            _isRefreshingDeviceStatus = false;
        }
    }

    // StartEmulator/StopEmulator kikommentelve: jelenleg valós, USB-n csatlakoztatott
    // Android eszközzel dolgozunk emulátor helyett, nincs szükség AVD-indításra.
    // Az enum-értékek és a driver-oldali logika megmaradnak, csak a UI-oldali
    // kényszerítő hivatkozásokat vettük ki, hogy StartEmulator nélkül is helyesen
    // működjön minden (alapértelmezett művelet, lokátor/érték-szükséglet stb.).
    private static readonly MobileStepAction[] NoLocatorActions =
    {
        //MobileStepAction.StartEmulator,
        MobileStepAction.LaunchApp, MobileStepAction.Swipe,
        MobileStepAction.Wait, MobileStepAction.Close,
        //MobileStepAction.StopEmulator
    };

    private static readonly MobileStepAction[] NoValueActions =
    {
        MobileStepAction.Click, MobileStepAction.LongPress, MobileStepAction.Clear,
        MobileStepAction.ScrollToElement, MobileStepAction.WaitVisible, MobileStepAction.WaitPresent,
        MobileStepAction.WaitAbsent, MobileStepAction.Wait, MobileStepAction.Close,
        //MobileStepAction.StopEmulator
    };

    private static readonly LocatorType[] SupportedLocatorTypes =
        { LocatorType.Id, LocatorType.XPath, LocatorType.ClassName, LocatorType.Name, LocatorType.AccessibilityId };

    public string Title => "Mobil (Android) tesztelés";
    public string Description => "";

    public ObservableCollection<TestStepRow> Steps { get; } = new();

    [ObservableProperty]
    private string testName = "";

    [ObservableProperty]
    private string selectedCategoryId = "";

    /// <summary>Csak az Android platformra engedélyezett kategóriák — lásd Beállítások, Teszt-kategóriák.</summary>
    public ObservableCollection<TestCategory> AvailableCategories { get; } = new();

    private void LoadAvailableCategories()
    {
        AvailableCategories.Clear();
        foreach (var category in _categoryService.GetCategoriesForTarget(AutomationTarget.Android))
            AvailableCategories.Add(category);

        if (AvailableCategories.All(c => c.Id != SelectedCategoryId))
            SelectedCategoryId = AvailableCategories.FirstOrDefault()?.Id ?? "";
    }

    partial void OnSelectedCategoryIdChanged(string value) => RunStepsCommand.NotifyCanExecuteChanged();

    public IReadOnlyList<MobileStepAction> AvailableActions { get; } = Enum.GetValues<MobileStepAction>();
    public IReadOnlyList<LocatorType> AvailableLocatorTypes { get; } = SupportedLocatorTypes;
    public IReadOnlyList<string> SwipeDirections { get; } = new[] { "Fel", "Le", "Balra", "Jobbra" };

    // Az alapértelmezett művelet StartEmulator helyett LaunchApp — mivel valós
    // eszközzel dolgozunk, a lépéssor jellemzően LaunchApp-pal kezdődik.
    // A mező (és a belőle generált NewAction property) NEM kommentelhető ki:
    // erre épül IsLocatorNeeded, IsValueNeeded, IsSwipeDirection, AddStep,
    // EditStep, CancelEdit és a lépésnév-építés is.
    [ObservableProperty]
    private MobileStepAction newAction = MobileStepAction.LaunchApp;

    [ObservableProperty]
    private LocatorType newLocatorType = LocatorType.Id;

    [ObservableProperty]
    private string newLocator = string.Empty;

    [ObservableProperty]
    private string newValue = string.Empty;

    [ObservableProperty]
    private int newTimeoutSeconds = 10;

    /// <summary>Ha a lokátor több elemre is illik (pl. egy lista minden sorában ugyanaz az
    /// AutomationId ismétlődik), ez adja meg, hányadik találattal dolgozzon a lépés
    /// — 1-alapú, EMBERI számozás (1 = első elem) — üresen az első találat. Szövegként tárolva, mert szabad
    /// szöveges beviteli mező; az AddStep-ben alakul TestStep.ElementIndex-szé.</summary>
    [ObservableProperty]
    private string newElementIndex = "";

    /// <summary>Hiba esetén ennyiszer próbálja újra a lépést, mielőtt véglegesen hibásnak
    /// jelölné — lásd TestStep.RetryCount.</summary>
    [ObservableProperty]
    private int newRetryCount;

    /// <summary>"Self-healing" tartalék lokátor — lásd TestStep.FallbackLocator.</summary>
    [ObservableProperty]
    private string newFallbackLocator = "";

    [ObservableProperty]
    private LocatorType newFallbackLocatorType = LocatorType.Id;

    /// <summary>Ha be van jelölve, a lépés hibája NEM szakítja meg a futtatást.</summary>
    [ObservableProperty]
    private bool newContinueOnError;

    /// <summary>Ha be van jelölve, a lépést a futtatás átugorja — meg sem kísérli végrehajtani.</summary>
    [ObservableProperty]
    private bool newSkip;

    /// <summary>A lépés saját azonosító címkéje — automatikusan generált, felülírható.</summary>
    [ObservableProperty]
    private string newLabel = "";

    /// <summary>Siker esetén ugrás célja (másik lépés Label-je) — üresen a normál, következő lépés jön.</summary>
    [ObservableProperty]
    private string? newOnSuccessGoToLabel;

    /// <summary>Hiba esetén ugrás célja (másik lépés Label-je) — üresen a ContinueOnError dönt.</summary>
    [ObservableProperty]
    private string? newOnFailureGoToLabel;

    /// <summary>A lépéslistában szereplő Label-ek + egy "— következő —" opció, ugrás-célpont választáshoz a ComboBox-okban.</summary>
    public IEnumerable<string> AvailableGoToLabels =>
        new[] { "" }.Concat(Steps.Select(s => s.Step.Label).Where(l => !string.IsNullOrWhiteSpace(l)));

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private BitmapImage? screenImage;

    [ObservableProperty]
    private bool isMirroring;

    [ObservableProperty]
    private bool isPicking;

    /// <summary>Igaz, amíg a Felvevő mód aktív — lásd ToggleRecordingCommand.</summary>
    [ObservableProperty]
    private bool isRecording;

    /// <summary>Igaz, ha az Élő kijelző önálló ablaka jelenleg látható. A fő nézet ez alapján
    /// dönti el, hogy mutassa-e az "Élő kijelző megnyitása" gombot.</summary>
    [ObservableProperty]
    private bool isMirrorWindowOpen;

    public ObservableCollection<LocatorCandidate> InspectorCandidates { get; } = new();

    public bool HasInspectorResult => InspectorCandidates.Count > 0;

    /// <summary>
    /// A lépéslistában kijelölt sor — sorra kattintva állítódik be (lásd MobileTestView.xaml,
    /// SelectStepCommand). A billentyűparancsok (Delete, Ctrl+D, Ctrl+↑/↓, F5-ös "Innentől"
    /// nincs erre kötve, mert az egy külön gomb, de a Delete/Duplikálás/Mozgatás igen) ezen
    /// keresztül tudják, melyik lépésre vonatkozzanak.
    /// </summary>
    [ObservableProperty]
    private TestStepRow? selectedStep;

    [RelayCommand]
    private void SelectStep(TestStepRow? row) => SelectedStep = row;

    private TestStepRow? _editingRow;

    public bool IsEditing => _editingRow is not null;
    public string AddButtonLabel => IsEditing ? "Mentés" : "Hozzáadás";

    public bool IsLocatorNeeded => !NoLocatorActions.Contains(NewAction);
    public bool IsValueNeeded => !NoValueActions.Contains(NewAction);
    public bool IsSwipeDirection => NewAction == MobileStepAction.Swipe;

    private readonly int _defaultTimeoutSeconds;
    private readonly string? _defaultAvdName;
    private readonly string? _defaultApkPath;

    public MobileTestViewModel(
        MobileAutomationDriver driver,
        INotificationService notificationService,
        AT.Infrastructure.ISettingsService settingsService,
        AT.Infrastructure.ITestSuiteFileService fileService,
        IMobileMirrorWindowService mirrorWindowService,
        ITestRunHistoryService historyService,
        ITestReportService reportService,
        IScheduledTaskService scheduledTaskService,
        ISchedulerService schedulerService,
        ITestCategoryService categoryService,
        IAndroidSdkInstallerService androidSdkInstallerService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
        _settingsService = settingsService;
        _mirrorWindowService = mirrorWindowService;
        _historyService = historyService;
        _reportService = reportService;
        _scheduledTaskService = scheduledTaskService;
        _schedulerService = schedulerService;
        _categoryService = categoryService;
        _androidSdkInstallerService = androidSdkInstallerService;
        _mirrorWindowService.Closed += OnMirrorWindowClosed;

        Steps.CollectionChanged += (_, _) =>
        {
            RunStepsCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AvailableGoToLabels));
        };

        _mirrorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _mirrorTimer.Tick += async (_, _) => await RefreshScreenAsync();

        // Az eszközállapot (Csatlakoztatva: ... sáv) automatikus, időzített frissítése —
        // 3 másodperces intervallum: elég gyors ahhoz, hogy csatlakoztatás/kihúzás/
        // engedélyezés után hamar frissüljön a UI, de nem terheli feleslegesen az adb-t
        // (minden tick egy külön "adb devices" + esetleg "adb shell getprop" folyamatindítás).
        _deviceStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _deviceStatusTimer.Tick += async (_, _) => await RefreshDeviceStatusAsync();

        var defaults = settingsService.Current;
        _defaultTimeoutSeconds = defaults.DefaultTimeoutSeconds;
        _defaultAvdName = defaults.DefaultAvdName;
        _defaultApkPath = defaults.DefaultApkPath;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
        _driver.SdkRootOverride = defaults.AndroidSdkRoot;

        LoadAvailableCategories();
        RefreshAndroidSdkStatus();
        _ = RefreshDeviceStatusAsync();
        _deviceStatusTimer.Start();

        // Az Élő kijelző ablak a nézet betöltésekor automatikusan megnyílik.
        OpenMirrorWindow();
    }

    partial void OnNewActionChanged(MobileStepAction value)
    {
        OnPropertyChanged(nameof(IsLocatorNeeded));
        OnPropertyChanged(nameof(IsValueNeeded));
        OnPropertyChanged(nameof(IsSwipeDirection));

        if (string.IsNullOrWhiteSpace(NewValue))
        {
            // StartEmulator-ági automatikus AVD-név-kitöltés kikommentelve —
            // lásd a fenti megjegyzést: emulátor helyett valós eszközzel dolgozunk.
            //if (value == MobileStepAction.StartEmulator && !string.IsNullOrWhiteSpace(_defaultAvdName))
            //    NewValue = _defaultAvdName;
            if (value == MobileStepAction.LaunchApp && !string.IsNullOrWhiteSpace(_defaultApkPath))
                NewValue = _defaultApkPath;
        }
    }

    // ===================== ÉLŐ KIJELZŐ ABLAK =====================

    /// <summary>
    /// Megnyitja (vagy előtérbe hozza) az Élő kijelző önálló ablakát. A ViewModel nem
    /// hoz létre WPF Window-t közvetlenül — ezt az IMobileMirrorWindowService végzi,
    /// aminek a DataContext-jeként saját magát (this) adja át.
    /// </summary>
    [RelayCommand]
    private void OpenMirrorWindow()
    {
        _mirrorWindowService.ShowOrActivate(this);
        IsMirrorWindowOpen = true;
    }

    /// <summary>
    /// A NavigationService hívja meg, mielőtt ezt a ViewModel-t lecseréli egy másikra
    /// (pl. a felhasználó egy másik oldalra navigál). A MobileTestViewModel Singleton,
    /// tehát ugyanaz a példány marad meg — itt csak azt kell leállítani, ami zavaró
    /// lenne, amíg a felhasználó másik oldalon van: a háttérben futó mirror- és
    /// eszközállapot-timert, és el kell rejteni a mirror-ablakot, hogy ne látszódjon
    /// feleslegesen.
    /// FONTOS: nem iratkozunk le a Closed eseményről itt — mivel ez a ViewModel
    /// Singleton, a konstruktorban történő feliratkozás egyszeri és végleges kell
    /// legyen; egy itteni leiratkozás visszavonhatatlanul megszüntetné a Closed
    /// figyelését minden jövőbeli visszanavigálás után.
    /// </summary>
    public void OnNavigatedFrom()
    {
        _mirrorTimer.Stop();
        _deviceStatusTimer.Stop();
        _mirrorWindowService.Hide();
    }

    private void OnMirrorWindowClosed(object? sender, EventArgs e) => IsMirrorWindowOpen = false;

    // ===================== ÉLŐ KIJELZŐ-TÜKRÖZÉS =====================
    // A régi DeviceDisplayManager egy sosem írt fájlt (device_screen.png) figyelt —
    // itt ténylegesen az Appium driver ad vissza egy valódi PNG-t minden ütemben.
    // Session nélkül (LaunchApp előtt) az adb-alapú TryGetScreenshotViaAdbAsync veszi
    // át a szerepet, amíg csak csatlakoztatott eszköz van, aktív Appium-session nincs.

    [RelayCommand]
    private async Task ToggleMirroring()
    {
        if (IsMirroring)
        {
            _mirrorTimer.Stop();
            IsMirroring = false;
            return;
        }

        if (!_driver.IsRunning)
            await RefreshDeviceStatusAsync();

        if (!_driver.IsRunning && !IsDeviceConnected)
        {
            _notificationService.Show("Nincs csatlakoztatott eszköz és nincs aktív session sem.", NotificationType.Warning);
            return;
        }

        _mirrorTimer.Start();
        IsMirroring = true;
    }

    private async Task RefreshScreenAsync()
    {
        byte[]? bytes;

        if (_driver.IsRunning)
        {
            bytes = await _driver.TryGetScreenshotAsync();
        }
        else if (IsDeviceConnected)
        {
            bytes = await _driver.TryGetScreenshotViaAdbAsync();
        }
        else
        {
            _mirrorTimer.Stop();
            IsMirroring = false;
            return;
        }

        if (bytes is null)
            return; // átmeneti hiba - egy frame-et kihagyunk, nem szakítjuk meg a tükrözést

        ScreenImage = ToBitmapImage(bytes);
    }

    private static BitmapImage ToBitmapImage(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ===================== LÉPÉSLISTA =====================

    [RelayCommand]
    private void AddStep()
    {
        if (IsLocatorNeeded && string.IsNullOrWhiteSpace(NewLocator))
        {
            _notificationService.Show("A lokátor mező kötelező ehhez a lépéstípushoz.", NotificationType.Warning);
            return;
        }

        // StartEmulator kikommentelve az ellenőrzésből — csak LaunchApp esetén
        // kötelező az Érték mező (az .apk elérési útja). Ha később visszaveszed
        // a StartEmulator-t használatba, a feltételt is vissza kell bővíteni:
        // (NewAction == MobileStepAction.StartEmulator || NewAction == MobileStepAction.LaunchApp)
        if (NewAction == MobileStepAction.LaunchApp && string.IsNullOrWhiteSpace(NewValue))
        {
            _notificationService.Show("Add meg az .apk elérési útját az érték mezőben.", NotificationType.Warning);
            return;
        }

        var step = new TestStep
        {
            Id = _editingRow?.Step.Id ?? Guid.NewGuid().ToString("N"),
            Name = BuildStepName(NewAction, NewLocator, NewValue),
            Target = AutomationTarget.Android,
            Action = NewAction.ToString(),
            Locator = NewLocator,
            LocatorType = NewLocatorType,
            Value = NewValue,
            TimeoutSeconds = NewTimeoutSeconds,
            ElementIndex = string.IsNullOrWhiteSpace(NewElementIndex) ? null
                : (int.TryParse(NewElementIndex, out var parsedIndex) ? parsedIndex : null),
            RetryCount = Math.Max(0, NewRetryCount),
            FallbackLocator = string.IsNullOrWhiteSpace(NewFallbackLocator) ? null : NewFallbackLocator,
            FallbackLocatorType = NewFallbackLocatorType,
            ContinueOnError = NewContinueOnError,
            Skip = NewSkip,
            Label = string.IsNullOrWhiteSpace(NewLabel)
                ? AT.Infrastructure.StepFlowResolver.GenerateNextLabel(Steps.Select(r => r.Step).ToList())
                : NewLabel,
            OnSuccessGoToLabel = string.IsNullOrWhiteSpace(NewOnSuccessGoToLabel) ? null : NewOnSuccessGoToLabel,
            OnFailureGoToLabel = string.IsNullOrWhiteSpace(NewOnFailureGoToLabel) ? null : NewOnFailureGoToLabel
        };

        if (_editingRow is not null)
        {
            _editingRow.Step = step;
            _editingRow.Status = TestStatus.NotRun;
            _editingRow.Message = null;
            _editingRow.Duration = null;
            _notificationService.Show("Lépés frissítve.", NotificationType.Success);
        }
        else
        {
            Steps.Add(new TestStepRow { Step = step });
        }

        CancelEdit();
    }

    [RelayCommand]
    private void EditStep(TestStepRow row)
    {
        _editingRow = row;

        NewAction = Enum.Parse<MobileStepAction>(row.Step.Action);
        NewLocatorType = row.Step.LocatorType;
        NewLocator = row.Step.Locator ?? string.Empty;
        NewValue = row.Step.Value ?? string.Empty;
        NewTimeoutSeconds = row.Step.TimeoutSeconds;
        NewElementIndex = row.Step.ElementIndex?.ToString() ?? "";
        NewRetryCount = row.Step.RetryCount;
        NewFallbackLocator = row.Step.FallbackLocator ?? string.Empty;
        NewFallbackLocatorType = row.Step.FallbackLocatorType;
        NewContinueOnError = row.Step.ContinueOnError;
        NewSkip = row.Step.Skip;
        NewLabel = row.Step.Label;
        NewOnSuccessGoToLabel = row.Step.OnSuccessGoToLabel;
        NewOnFailureGoToLabel = row.Step.OnFailureGoToLabel;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddButtonLabel));
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingRow = null;

        // StartEmulator helyett LaunchApp az alapértelmezett visszaállási érték is,
        // hogy szerkesztés/megszakítás után is a valós-eszközös munkafolyamathoz
        // illeszkedő művelet legyen kiválasztva.
        NewAction = MobileStepAction.LaunchApp;
        NewLocator = string.Empty;
        NewValue = string.Empty;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
        NewElementIndex = "";
        NewRetryCount = 0;
        NewFallbackLocator = "";
        NewFallbackLocatorType = LocatorType.Id;
        NewContinueOnError = false;
        NewSkip = false;
        NewLabel = "";
        NewOnSuccessGoToLabel = null;
        NewOnFailureGoToLabel = null;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddButtonLabel));
    }

    /// <summary>
    /// "Új teszt" — teljesen letisztázza a nézetet: kiüríti a lépéslistát, a teszt
    /// nevét, és megszakít egy esetleg folyamatban lévő szerkesztést, hogy a felhasználó
    /// nulláról kezdhessen egy új lépéssorozatot. Ha már van felvett lépés, előbb
    /// megerősítést kér, hogy véletlenül ne veszítsen el munkát.
    /// </summary>
    [RelayCommand]
    private void NewTest()
    {
        if (Steps.Count > 0)
        {
            var confirmed = AT.App.Views.ConfirmDialog.Show(
                Application.Current.MainWindow,
                "Új teszt",
                "Biztosan törlöd a jelenlegi lépéssort? A nem mentett lépések elvesznek.",
                confirmButtonText: "Törlés",
                isDestructive: true);

            if (!confirmed)
                return;
        }

        CancelEdit();
        Steps.Clear();
        TestName = "";
        LoadAvailableCategories();
        _notificationService.Show("Új, üres lépéssor létrehozva.", NotificationType.Info);
    }

    [RelayCommand]
    private async Task ScheduleTaskAsync()
    {
        if (Steps.Count == 0)
        {
            _notificationService.Show("Nincs felvett lépés — előbb vegyél fel legalább egy lépést.", NotificationType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCategoryId))
        {
            _notificationService.Show("Válassz kategóriát az ütemezés létrehozása előtt.", NotificationType.Warning);
            return;
        }

        var task = AT.App.Views.ScheduleTaskDialog.Show(
            Application.Current.MainWindow,
            TestName,
            SelectedCategoryId,
            AutomationTarget.Android,
            Steps.Select(r => r.Step).ToList());

        if (task is null)
            return;

        await _scheduledTaskService.AddAsync(task);
        _schedulerService.RecalculateNextRun(task);
        await _scheduledTaskService.UpdateAsync(task);

        _notificationService.Show("Ütemezés létrehozva.", NotificationType.Success);
    }

    [RelayCommand]
    private void RemoveStep(TestStepRow row)
    {
        if (_editingRow == row)
            CancelEdit();

        Steps.Remove(row);
    }

    [RelayCommand]
    private void MoveStepUp(TestStepRow row)
    {
        var index = Steps.IndexOf(row);
        if (index > 0)
            Steps.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveStepDown(TestStepRow row)
    {
        var index = Steps.IndexOf(row);
        if (index >= 0 && index < Steps.Count - 1)
            Steps.Move(index, index + 1);
    }

    /// <summary>
    /// Egy lépés áthelyezése tetszőleges pozícióra — a drag&amp;drop átrendezéshez
    /// (lásd MobileTestView.xaml.cs). A ↑/↓ gombokkal ellentétben ez nem csak
    /// szomszédos cserét végez, hanem a lista bármely pontjára mozgathat egy lépést.
    /// </summary>
    public void MoveStepTo(TestStepRow row, int targetIndex)
    {
        var currentIndex = Steps.IndexOf(row);
        if (currentIndex < 0)
            return;

        targetIndex = Math.Clamp(targetIndex, 0, Steps.Count - 1);
        if (currentIndex == targetIndex)
            return;

        Steps.Move(currentIndex, targetIndex);
    }

    [RelayCommand]
    private void DuplicateStep(TestStepRow row)
    {
        var original = row.Step;
        var copy = new TestStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = original.Name,
            Target = original.Target,
            Action = original.Action,
            Locator = original.Locator,
            LocatorType = original.LocatorType,
            Value = original.Value,
            TargetLocator = original.TargetLocator,
            TargetLocatorType = original.TargetLocatorType,
            TimeoutSeconds = original.TimeoutSeconds,
            ContinueOnError = original.ContinueOnError,
            Skip = original.Skip
        };

        var index = Steps.IndexOf(row);
        Steps.Insert(index + 1, new TestStepRow { Step = copy });
        _notificationService.Show("Lépés duplikálva.", NotificationType.Success);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunStepsAsync() => RunStepsCoreAsync(startIndex: 0);

    /// <summary>
    /// "Futtatás innentől" — a kijelölt lépéstől kezdve fut le a sor vége felé, a
    /// megelőző lépéseket kihagyva. Hasznos hibakereséskor, ha nem szeretnéd az egész
    /// sort újra lefuttatni egyetlen lépés ellenőrzéséhez. A driver session-jét (StartAsync)
    /// ilyenkor is elindítjuk, mert enélkül a köztes lépések előfeltételei hiányoznának.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunFromStepAsync(TestStepRow? row)
    {
        var startIndex = row is null ? 0 : Steps.IndexOf(row);
        if (startIndex < 0)
            startIndex = 0;

        return RunStepsCoreAsync(startIndex);
    }

    private async Task RunStepsCoreAsync(int startIndex)
    {
        if (string.IsNullOrWhiteSpace(SelectedCategoryId))
        {
            _notificationService.Show("Válassz kategóriát a teszt futtatása előtt.", NotificationType.Warning);
            return;
        }

        // Proaktív SDK-ellenőrzés a futtatás elindítása ELŐTT — enélkül a felhasználó
        // csak akkor szembesülne a hiányzó Android SDK-val, amikor egy StartEmulator/
        // LaunchApp lépés menet közben elhasal, ami sokkal kevésbé egyértelmű, mint egy
        // azonnali, konkrét figyelmeztetés még azelőtt, hogy bármi elindulna.
        var missingSdkReason = AT.Automation.Mobile.AndroidSdkLocator.TryDescribeMissingSdk(_settingsService.Current.AndroidSdkRoot);
        if (missingSdkReason is not null)
        {
            _notificationService.Show(missingSdkReason, NotificationType.Error);
            return;
        }

        IsRunning = true;

        _schedulerService.SetModuleBusy(AutomationTarget.Android, true);

        RunStepsCommand.NotifyCanExecuteChanged();

        var startedAt = DateTime.Now;
        _currentRunScreenshotFolder = ResolveRunScreenshotFolder(startedAt);

        try
        {
            await _driver.StartAsync();

            var stepList = Steps.Select(r => r.Step).ToList();
            var currentIndex = startIndex;
            var executionCount = 0;
            var hitExecutionLimit = false;

            while (currentIndex is >= 0 && currentIndex < Steps.Count)
            {
                executionCount++;
                if (executionCount > AT.Infrastructure.StepFlowResolver.MaxStepExecutions)
                {
                    hitExecutionLimit = true;
                    break;
                }

                var row = Steps[currentIndex];

                row.Message = null;
                row.Duration = null;
                row.ScreenshotPath = null;

                if (row.Step.Skip)
                {
                    row.Status = TestStatus.Skipped;
                    currentIndex++;
                    continue;
                }

                row.Status = TestStatus.Running;
                var stopwatch = Stopwatch.StartNew();
                bool wasSuccess;

                // A RetryCount 0 esetén pontosan 1 kísérletet jelent (a régi viselkedéssel
                // megegyezően) — maxAttempts = RetryCount + 1. A ciklus csak akkor dob
                // tovább/jelöl Failed-nek, ha az UTOLSÓ kísérlet is elhasal; a köztes
                // sikertelen kísérletek csendben, egy figyelmeztető toast-tal jeleznek,
                // és azonnal újrapróbálkoznak (a stopwatch a teljes, összes kísérletet
                // átfogó időt méri, hogy a riportban lásd, mennyi ideig "küzdött" a lépés).
                var maxAttempts = Math.Max(1, row.Step.RetryCount + 1);
                var attempt = 0;
                string? lastErrorMessage = null;

                while (true)
                {
                    attempt++;
                    try
                    {
                        var result = await _driver.ExecuteStepAsync(row.Step);
                        stopwatch.Stop();
                        row.Duration = stopwatch.Elapsed;
                        row.Status = TestStatus.Passed;
                        await CaptureScreenshotIfNeededAsync(row, isFailure: false);
                        wasSuccess = true;

                        if (!string.IsNullOrEmpty(result))
                            _notificationService.Show($"{row.Step.Name} → {result}", NotificationType.Info);

                        if (attempt > 1)
                            _notificationService.Show($"{row.Step.Name} — sikerült a(z) {attempt}. próbálkozásra.", NotificationType.Info);

                        // "Self-healing" jelzés — az elsődleges lokátor nem volt megtalálható,
                        // de a tartalék igen. Ez akkor is fut, ha attempt == 1 (első próbálkozásra
                        // is a fallback találta meg), mert ez a driver oldali, nem a retry oldali
                        // jelzés — érdemes frissíteni az elsődleges lokátort a lépésben.
                        if (_driver.LastStepUsedFallbackLocator)
                            _notificationService.Show($"{row.Step.Name} — az elsődleges lokátor nem volt megtalálható, a tartalék lokátorral sikerült. Érdemes frissíteni az elsődleges lokátort.", NotificationType.Warning);

                        break;
                    }
                    catch (Exception ex)
                    {
                        lastErrorMessage = ex.Message;

                        if (attempt < maxAttempts)
                        {
                            _notificationService.Show(
                                $"{row.Step.Name} — {attempt}. próbálkozás sikertelen ({ex.Message}), újrapróbálás ({maxAttempts - attempt} van hátra)…",
                                NotificationType.Warning);
                            await Task.Delay(300);
                            continue;
                        }

                        stopwatch.Stop();
                        row.Duration = stopwatch.Elapsed;
                        row.Status = TestStatus.Failed;
                        row.Message = attempt > 1 ? $"{lastErrorMessage} ({attempt} próbálkozás után)" : lastErrorMessage;

                        // A hiba pontos szövegét (pl. "Nem található Android SDK...") a toast
                        // is megkapja, nem csak a lépés-sor alatti, könnyen elnézhető apró
                        // szöveg — enélkül a felhasználó csak annyit látna, hogy "Lépés
                        // sikertelen", a tényleges, cselekvésre ösztönző okot meg kellene
                        // keresnie a lépéslistában.
                        _notificationService.Show(
                            $"Lépés sikertelen: {row.Step.Name} — {ex.Message}" + (attempt > 1 ? $" ({attempt} próbálkozás után)" : ""),
                            NotificationType.Error);

                        await CaptureScreenshotIfNeededAsync(row, isFailure: true);
                        wasSuccess = false;
                        break;
                    }
                }

                var nextIndex = AT.Infrastructure.StepFlowResolver.ResolveNextIndex(
                    stepList, currentIndex, wasSuccess, row.Step.ContinueOnError, out var shouldStop);

                if (shouldStop)
                    break;

                currentIndex = nextIndex ?? Steps.Count;
            }

            if (hitExecutionLimit)
            {
                _notificationService.Show(
                    $"A futtatás leállt: több mint {AT.Infrastructure.StepFlowResolver.MaxStepExecutions} lépés futott le — valószínűleg végtelen ciklusba került az ugrások miatt.",
                    NotificationType.Error);
            }

            var hasFailed = Steps.Any(s => s.Status == TestStatus.Failed);
            _notificationService.Show(
                hasFailed ? "A futtatás hibával leállt (vagy folytatódott a beállítás szerint)." : "Minden lépés sikeresen lefutott.",
                hasFailed ? NotificationType.Error : NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Hiba a futtatás közben: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsRunning = false;
            
            RunStepsCommand.NotifyCanExecuteChanged();

            _schedulerService.SetModuleBusy(AutomationTarget.Android, false);

            await SaveRunToHistoryAsync(startedAt, DateTime.Now);
        }
    }

    /// <summary>
    /// Létrehozza (ha a Beállítások szerint egyáltalán készül kép) a futtatáshoz tartozó,
    /// a teszt nevét és időbélyeget tartalmazó almappát. Null-t ad vissza, ha a screenshot
    /// mód "Soha" — ilyenkor sem mappa, sem kép nem jön létre.
    /// </summary>
    private string? ResolveRunScreenshotFolder(DateTime startedAt)
    {
        if (_settingsService.Current.ScreenshotCaptureMode == AT.Infrastructure.ScreenshotCaptureMode.Never)
            return null;

        var baseFolder = string.IsNullOrWhiteSpace(_settingsService.Current.ScreenshotFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _settingsService.Current.ScreenshotFolderPath!;

        return ScreenshotFolderResolver.CreateRunFolder(baseFolder, TestName, startedAt);
    }

    /// <summary>Összeállítja és elmenti a futtatás összegzését a közös history-tárolóba, majd riport-exportálhatóvá teszi.</summary>
    private async Task SaveRunToHistoryAsync(DateTime startedAt, DateTime finishedAt)
    {
        var record = new TestRunRecord
        {
            TestName = TestName,
            CategoryId = SelectedCategoryId,
            Target = AutomationTarget.Android,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            TotalSteps = Steps.Count,
            PassedCount = Steps.Count(s => s.Status == TestStatus.Passed),
            FailedCount = Steps.Count(s => s.Status == TestStatus.Failed),
            SkippedCount = Steps.Count(s => s.Status == TestStatus.Skipped),
            ScreenshotFolderPath = _currentRunScreenshotFolder,
            StepResults = Steps.Select(s => new TestStepResult
            {
                StepName = s.Step.Name,
                Status = s.Status,
                Duration = s.Duration,
                Message = s.Message,
                ScreenshotPath = s.ScreenshotPath
            }).ToList()
        };

        _lastRunRecord = record;
        OnPropertyChanged(nameof(HasLastRun));

        try
        {
            await _historyService.SaveRunAsync(record);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Előzmény mentése sikertelen: {ex.Message}", NotificationType.Warning);
        }
    }

    /// <summary>A legutóbbi futtatás HTML riportjának exportálása fájlba, majd megnyitása böngészőben.</summary>
    [RelayCommand]
    private void ExportReport()
    {
        if (_lastRunRecord is null)
        {
            _notificationService.Show("Még nincs futtatási eredmény, amiből riportot lehetne készíteni.", NotificationType.Warning);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Riport exportálása",
            Filter = "HTML fájl (*.html)|*.html",
            DefaultExt = ".html",
            FileName = string.IsNullOrWhiteSpace(TestName) ? "mobil-riport.html" : $"{TestName}-riport.html"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var html = _reportService.GenerateHtml(_lastRunRecord);
            File.WriteAllText(dialog.FileName, html);
            _notificationService.Show("Riport elmentve.", NotificationType.Success);

            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Riport exportálása sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    private bool CanRun() => !IsRunning && Steps.Count > 0 && !string.IsNullOrWhiteSpace(SelectedCategoryId) && !IsAndroidSdkMissing;

    /// <summary>A Beállításokban választott mód szerint (soha / csak hiba / minden lépés) ment képernyőképet,
    /// a futtatáshoz tartozó, ResolveRunScreenshotFolder által létrehozott almappába.</summary>
    private async Task CaptureScreenshotIfNeededAsync(TestStepRow row, bool isFailure)
    {
        var mode = _settingsService.Current.ScreenshotCaptureMode;
        var shouldCapture = mode == AT.Infrastructure.ScreenshotCaptureMode.Always
            || (isFailure && mode == AT.Infrastructure.ScreenshotCaptureMode.OnErrorOnly);

        if (!shouldCapture || _currentRunScreenshotFolder is null)
            return;

        try
        {
            var bytes = await _driver.GetScreenshotAsync();

            var fileName = $"{SanitizeFileName(row.Step.Name)}_{DateTime.Now:HHmmss_fff}.png";
            var fullPath = Path.Combine(_currentRunScreenshotFolder, fileName);

            await File.WriteAllBytesAsync(fullPath, bytes);
            row.ScreenshotPath = fullPath;

            if (isFailure)
                _notificationService.Show($"Képernyőkép mentve: {fullPath}", NotificationType.Info);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Képernyőkép mentése sikertelen: {ex.Message}", NotificationType.Warning);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    [RelayCommand]
    private async Task SaveStepsAsync()
    {
        if (Steps.Count == 0)
        {
            _notificationService.Show("Nincs menthető lépés.", NotificationType.Warning);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Lépéssor mentése",
            Filter = "XML fájl (*.xml)|*.xml",
            DefaultExt = ".xml",
            FileName = string.IsNullOrWhiteSpace(TestName) ? "mobil-lepesek.xml" : $"{TestName}.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _fileService.SaveAsync(dialog.FileName, AutomationTarget.Android, Steps.Select(r => r.Step), TestName, SelectedCategoryId);
            _notificationService.Show("Lépéssor elmentve.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Mentés sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task LoadStepsAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Lépéssor betöltése",
            Filter = "XML fájl (*.xml)|*.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var file = await _fileService.LoadAsync(dialog.FileName, AutomationTarget.Android);

            Steps.Clear();
            foreach (var dto in file.Steps)
                Steps.Add(new TestStepRow { Step = AT.Infrastructure.TestSuiteMapper.ToTestStep(dto, AutomationTarget.Android) });

            TestName = file.Name ?? "";

            LoadAvailableCategories();
            if (!string.IsNullOrWhiteSpace(file.CategoryId) && AvailableCategories.Any(c => c.Id == file.CategoryId))
                SelectedCategoryId = file.CategoryId;

            CancelEdit();
            _notificationService.Show($"{file.Steps.Count} lépés betöltve.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Betöltés sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task TogglePicking()
    {
        if (!_driver.IsRunning)
            await RefreshDeviceStatusAsync();

        if (!_driver.IsRunning && !IsDeviceConnected)
        {
            _notificationService.Show("Nincs csatlakoztatott eszköz és nincs aktív session sem.", NotificationType.Warning);
            return;
        }

        IsPicking = !IsPicking;
        if (IsPicking)
            IsRecording = false; // a két mód kölcsönösen kizárja egymást
        InspectorCandidates.Clear();
        OnPropertyChanged(nameof(HasInspectorResult));

        if (IsPicking && !IsMirroring)
            await ToggleMirroring();
    }

    /// <summary>Az élő kijelző képére kattintva hívja a code-behind (relatív, 0..1 koordinátával).
    /// Aktív Appium-session esetén az Appium PageSource-t, egyébként (session nélkül, csak
    /// csatlakoztatott eszközzel) a közvetlen adb-alapú uiautomator dump-ot használja.
    /// Felvevő módban (IsRecording) a talált elemet NEM jelölt-listaként mutatja meg
    /// kiválasztásra, hanem AZONNAL, automatikusan hozzáad egy "Kattintás" lépést —
    /// ez az asszisztált felvétel lényege: a mobil oldalon nincs natív "hallgasd az
    /// érintéseket" mechanizmus, ezért minden rögzítendő kattintást a felhasználónak
    /// magán az élő kijelzőn kell megtennie (nem a telefonon közvetlenül).</summary>
    public async Task CaptureElementAtAsync(double relativeX, double relativeY)
    {
        if ((!IsPicking && !IsRecording) || relativeX is < 0 or > 1 || relativeY is < 0 or > 1)
            return;

        var info = _driver.IsRunning
            ? await _driver.GetElementAtRelativePointAsync(relativeX, relativeY)
            : await _driver.GetElementAtRelativePointViaAdbAsync(relativeX, relativeY);

        if (info is null)
        {
            _notificationService.Show("Nem található elem ezen a ponton.", NotificationType.Warning);
            if (IsPicking)
            {
                InspectorCandidates.Clear();
                OnPropertyChanged(nameof(HasInspectorResult));
            }
            return;
        }

        if (IsRecording)
        {
            AddRecordedClickStep(info);
            return;
        }

        InspectorCandidates.Clear();

        AddInspectorCandidate(LocatorType.Id, "resource-id", info.ResourceId, info.ResourceIdMatchIndex, info.ResourceIdMatchCount);
        AddInspectorCandidate(LocatorType.AccessibilityId, "content-desc", info.ContentDesc, info.ContentDescMatchIndex, info.ContentDescMatchCount);
        AddInspectorCandidate(LocatorType.ClassName, "class", info.ClassName, info.ClassNameMatchIndex, info.ClassNameMatchCount);

        OnPropertyChanged(nameof(HasInspectorResult));

        if (!HasInspectorResult)
            _notificationService.Show("Az elemnek nincs használható azonosítója.", NotificationType.Warning);
    }

    /// <summary>Felvevő módban a kattintott elemből közvetlenül épít egy "Click" TestStep-et,
    /// és hozzáadja a listához — ugyanazt a prioritási sorrendet követi, mint amit a
    /// felhasználó kézzel is választana a jelöltek közül (content-desc > resource-id >
    /// class), csak automatikusan, a legjobb elérhető lokátort választva.</summary>
    private void AddRecordedClickStep(MobileElementInfo info)
    {
        string locator;
        LocatorType locatorType;
        int? elementIndex = null;

        if (!string.IsNullOrWhiteSpace(info.ContentDesc))
        {
            locator = info.ContentDesc;
            locatorType = LocatorType.AccessibilityId;
            if (info.ContentDescMatchCount > 1) elementIndex = info.ContentDescMatchIndex;
        }
        else if (!string.IsNullOrWhiteSpace(info.ResourceId))
        {
            locator = info.ResourceId;
            locatorType = LocatorType.Id;
            if (info.ResourceIdMatchCount > 1) elementIndex = info.ResourceIdMatchIndex;
        }
        else if (!string.IsNullOrWhiteSpace(info.ClassName))
        {
            locator = info.ClassName;
            locatorType = LocatorType.ClassName;
            if (info.ClassNameMatchCount > 1) elementIndex = info.ClassNameMatchIndex;
        }
        else
        {
            _notificationService.Show("Az elemnek nincs használható azonosítója — kihagyva.", NotificationType.Warning);
            return;
        }

        _lastRecordedLocator = locator;
        _lastRecordedLocatorType = locatorType;
        _lastRecordedElementIndex = elementIndex;

        var step = new TestStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"Kattintás → {locator}",
            Target = AutomationTarget.Android,
            Action = MobileStepAction.Click.ToString(),
            Locator = locator,
            LocatorType = locatorType,
            ElementIndex = elementIndex,
            TimeoutSeconds = _defaultTimeoutSeconds,
            Label = AT.Infrastructure.StepFlowResolver.GenerateNextLabel(Steps.Select(r => r.Step).ToList())
        };

        Steps.Add(new TestStepRow { Step = step });
        _notificationService.Show($"Rögzítve: Kattintás → {locator}", NotificationType.Success);
    }

    /// <summary>Felvevő módban az utolsó rögzített kattintás lokátorát tároljuk el, hogy
    /// a "Szöveg rögzítése" gomb (lásd RecordingTextInput/AddRecordedTextCommand) tudja,
    /// melyik elemre vonatkozzon a SendKeys lépés.</summary>
    private string? _lastRecordedLocator;
    private LocatorType _lastRecordedLocatorType;
    private int? _lastRecordedElementIndex;

    /// <summary>Felvevő módban a szövegbeviteli mezőkbe írandó szöveg gyors rögzítéséhez —
    /// mivel a mobil oldalon nincs natív "hallgasd a begépelt karaktereket" mechanizmus,
    /// a felhasználó ide írja be a szöveget, ami a "Szöveg rögzítése" gombra kattintva
    /// SendKeys lépésként kerül be, az UTOLJÁRA rögzített kattintás lokátorát célozva
    /// (a tipikus munkafolyamat: kattints a mezőre az élő kijelzőn → írd be ide a
    /// szöveget → "Szöveg rögzítése").</summary>
    [ObservableProperty]
    private string recordingTextInput = "";

    [RelayCommand]
    private void AddRecordedText()
    {
        if (string.IsNullOrWhiteSpace(RecordingTextInput))
            return;

        if (_lastRecordedLocator is null)
        {
            _notificationService.Show("Előbb kattints egy mezőre az élő kijelzőn, hogy legyen mihez rendelni a szöveget.", NotificationType.Warning);
            return;
        }

        var step = new TestStep
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"Szöveg beírása → {_lastRecordedLocator} → {RecordingTextInput}",
            Target = AutomationTarget.Android,
            Action = MobileStepAction.SendKeys.ToString(),
            Locator = _lastRecordedLocator,
            LocatorType = _lastRecordedLocatorType,
            ElementIndex = _lastRecordedElementIndex,
            Value = RecordingTextInput,
            TimeoutSeconds = _defaultTimeoutSeconds,
            Label = AT.Infrastructure.StepFlowResolver.GenerateNextLabel(Steps.Select(r => r.Step).ToList())
        };

        Steps.Add(new TestStepRow { Step = step });
        RecordingTextInput = "";
    }

    /// <summary>
    /// Felvevő mód be-/kikapcsolása. Bekapcsoláskor — ha még nincs aktív session vagy
    /// csatlakoztatott eszköz — figyelmezteti a felhasználót, hogy előbb fusson le egy
    /// LaunchApp lépés. Automatikusan elindítja az élő kijelző tükrözését is (ha még
    /// nem aktív), mert a felvétel MAGÁN az élő kijelzőn történő kattintásokra épül.
    /// </summary>
    [RelayCommand]
    private async Task ToggleRecording()
    {
        if (IsRecording)
        {
            IsRecording = false;
            _lastRecordedLocator = null;
            RecordingTextInput = "";
            _notificationService.Show("Felvétel leállítva.", NotificationType.Info);
            return;
        }

        if (!_driver.IsRunning)
            await RefreshDeviceStatusAsync();

        if (!_driver.IsRunning && !IsDeviceConnected)
        {
            _notificationService.Show("Nincs csatlakoztatott eszköz és nincs aktív session sem — előbb futtass egy 'LaunchApp' lépést, vagy csatlakoztass egy telefont.", NotificationType.Warning);
            return;
        }

        IsRecording = true;
        IsPicking = false;
        InspectorCandidates.Clear();
        OnPropertyChanged(nameof(HasInspectorResult));

        if (!IsMirroring)
            await ToggleMirroring();

        _notificationService.Show(
            "Felvétel elindult — kattints az élő kijelzőn a kívánt elemekre; minden kattintás automatikusan 'Kattintás' lépést ad hozzá. " +
            "Szövegbeviteli mezőnél a kattintás után írd be a szöveget a felvétel-panel mezőjébe, majd 'Szöveg rögzítése'.",
            NotificationType.Success);
    }

    /// <summary>matchCount &gt; 1 esetén a Label-hez hozzáfűzi a "lokátor N. eleme (összesen M)"
    /// jelzést,
    /// és a jelöltbe belekerül a MatchIndex/MatchCount is — ez adja meg a felhasználónak
    /// (és a lépésbe automatikusan beillesztve az ElementIndex-et), hányadik egyező
    /// elemre kattintott, amikor a lokátor nem egyedi.</summary>
    private void AddInspectorCandidate(LocatorType type, string label, string? value, int matchIndex = 0, int matchCount = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var suffix = matchCount > 1 ? $" — lokátor {matchIndex}. eleme (összesen {matchCount})" : "";
        InspectorCandidates.Add(new LocatorCandidate
        {
            Type = type,
            Label = label + suffix,
            Value = value,
            MatchIndex = matchCount > 1 ? matchIndex : null,
            MatchCount = matchCount > 1 ? matchCount : null
        });
    }

    [RelayCommand]
    private void UseInspectorCandidate(LocatorCandidate? candidate)
    {
        if (candidate is null)
            return;

        NewLocatorType = candidate.Type;
        NewLocator = candidate.Value;
        NewElementIndex = candidate.MatchIndex?.ToString() ?? "";
        InspectorCandidates.Clear();
        OnPropertyChanged(nameof(HasInspectorResult));
        IsPicking = false;

        var indexNote = candidate.MatchIndex.HasValue ? $" (elem sorszáma: {candidate.MatchIndex})" : "";
        _notificationService.Show($"Lokátor beillesztve az elem-keresőből{indexNote}.", NotificationType.Success);
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        _mirrorTimer.Stop();
        IsMirroring = false;
        ScreenImage = null;
        await _driver.StopAsync();
        _notificationService.Show("Session és Appium szerver leállítva.", NotificationType.Info);
    }

    // ===================== BILLENTYŰPARANCSOK =====================
    // A MobileTestView.xaml.cs PreviewKeyDown-ja hívja meg ezeket, a SelectedStep-et
    // használva "a kijelölt lépés" gyanánt. A metódusok szándékosan tolerálják a
    // hiányzó kijelölést (null SelectedStep esetén egyszerűen nem csinálnak semmit),
    // hogy a billentyűparancs lenyomása sose dobjon hibát, ha épp nincs kijelölt sor.

    /// <summary>Ctrl+S — a kijelölt lépéstől függetlenül mindig a teljes lépéssort menti.</summary>
    public void HandleSaveShortcut() => SaveStepsCommand.Execute(null);

    /// <summary>Ctrl+O — lépéssor betöltése.</summary>
    public void HandleLoadShortcut() => LoadStepsCommand.Execute(null);

    /// <summary>F5 — teljes futtatás az elejétől.</summary>
    public void HandleRunShortcut()
    {
        if (RunStepsCommand.CanExecute(null))
            RunStepsCommand.Execute(null);
    }

    /// <summary>Shift+F5 — leállítás.</summary>
    public void HandleStopShortcut() => StopAllCommand.Execute(null);

    /// <summary>Delete — a kijelölt lépés törlése.</summary>
    public void HandleDeleteShortcut()
    {
        if (SelectedStep is { } row)
            RemoveStepCommand.Execute(row);
    }

    /// <summary>Ctrl+D — a kijelölt lépés duplikálása.</summary>
    public void HandleDuplicateShortcut()
    {
        if (SelectedStep is { } row)
            DuplicateStepCommand.Execute(row);
    }

    /// <summary>Ctrl+↑ — a kijelölt lépés feljebb mozgatása.</summary>
    public void HandleMoveUpShortcut()
    {
        if (SelectedStep is { } row)
            MoveStepUpCommand.Execute(row);
    }

    /// <summary>Ctrl+↓ — a kijelölt lépés lejjebb mozgatása.</summary>
    public void HandleMoveDownShortcut()
    {
        if (SelectedStep is { } row)
            MoveStepDownCommand.Execute(row);
    }

    /// <summary>Esc — folyamatban lévő szerkesztés megszakítása.</summary>
    public void HandleEscapeShortcut()
    {
        if (IsEditing)
            CancelEditCommand.Execute(null);
    }

    private static string BuildStepName(MobileStepAction action, string locator, string value) => action switch
    {
        // StartEmulator/StopEmulator case-ek kikommentelve — az enum-értékek
        // megmaradnak (lásd MobileStepAction.cs), csak a UI-oldali lépésnév-építést
        // vettük ki, mivel ezekre a lépéstípusokra jelenleg nincs szükség.
        //MobileStepAction.StartEmulator => $"Emulátor indítása → {value}",
        MobileStepAction.LaunchApp => $"Alkalmazás telepítése/indítása → {value}",
        MobileStepAction.Click => $"Kattintás → {locator}",
        MobileStepAction.LongPress => $"Hosszan nyomás → {locator}",
        MobileStepAction.SendKeys => $"Szöveg beírása → {locator} → {value}",
        MobileStepAction.Clear => $"Mező ürítése → {locator}",
        MobileStepAction.Swipe => $"Húzás → {value}",
        MobileStepAction.ScrollToElement => $"Görgetés az elemig → {locator}",
        MobileStepAction.ReadAttribute => $"Attribútum kiolvasása → {locator} → {value}",
        MobileStepAction.Wait => "Várakozás",
        MobileStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        MobileStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        MobileStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        MobileStepAction.WaitHasText => $"Várakozás szövegre → {locator} → {value}",
        MobileStepAction.WaitHasAttribute => $"Várakozás attribútumra → {locator} → {value}",
        MobileStepAction.Close => "Alkalmazás bezárása",
        //MobileStepAction.StopEmulator => "Emulátor leállítása",
        _ => action.ToString()
    };
}
