using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Web;
using AT.Core.Contracts;
using AT.Core.Models;
using AT.Infrastructure;
using AT.App.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class WebTestViewModel : ObservableObject
{
    private readonly WebAutomationDriver _driver;
    private readonly INotificationService _notificationService;
    private readonly AT.Infrastructure.ITestSuiteFileService _fileService;
    private readonly AT.Infrastructure.ISettingsService _settingsService;
    private readonly ITestRunHistoryService _historyService;
    private readonly ITestReportService _reportService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ISchedulerService _schedulerService;
    private readonly ITestCategoryService _categoryService;
    private readonly DispatcherTimer _recordingTimer;

    // A folyamatban lévő (vagy legutóbb befejezett) futtatás képernyőkép-mappája — null, ha ehhez a futtatáshoz nem készül kép.
    private string? _currentRunScreenshotFolder;

    // A legutóbbi futtatás összegzése — a "Riport exportálása" gomb ezt írja ki HTML-be.

    private TestRunRecord? _lastRunRecord;

    public bool HasLastRun => _lastRunRecord is not null;

    private static readonly WebStepAction[] NoLocatorActions = { WebStepAction.Navigate, WebStepAction.Wait };
    private static readonly WebStepAction[] NoValueActions =
    {
        WebStepAction.Click, WebStepAction.DoubleClick, WebStepAction.RightClick, WebStepAction.Hover,
        WebStepAction.Clear, WebStepAction.WaitVisible, WebStepAction.WaitClickable,
        WebStepAction.WaitPresent, WebStepAction.WaitAbsent, WebStepAction.DragAndDrop
    };

    public string Title => "Web tesztelés";
    public string Description => "";

    public ObservableCollection<TestStepRow> Steps { get; } = new();

    [ObservableProperty]
    private string testName = "";

    [ObservableProperty]
    private string selectedCategoryId = "";

    // Csak a Web platformra engedélyezett kategóriák — lásd Beállítások, Teszt-kategóriák.
    public ObservableCollection<TestCategory> AvailableCategories { get; } = new();

    private void LoadAvailableCategories()
    {
        AvailableCategories.Clear();
        foreach (var category in _categoryService.GetCategoriesForTarget(AutomationTarget.Web))
            AvailableCategories.Add(category);

        // Ha a jelenleg kiválasztott kategória már nem szerepel a listában (pl. törölték,
        // vagy még nincs kiválasztva), az első elérhetőre esünk vissza — enélkül a
        // felhasználó egy érvénytelen/üres SelectedCategoryId-vel maradna.

        if (AvailableCategories.All(c => c.Id != SelectedCategoryId))
            SelectedCategoryId = AvailableCategories.FirstOrDefault()?.Id ?? "";
    }

    partial void OnSelectedCategoryIdChanged(string value) => RunStepsCommand.NotifyCanExecuteChanged();

    public IReadOnlyList<BrowserType> AvailableBrowsers { get; } = Enum.GetValues<BrowserType>();
    public IReadOnlyList<WebStepAction> AvailableActions { get; } = Enum.GetValues<WebStepAction>();
    public IReadOnlyList<LocatorType> AvailableLocatorTypes { get; } =
        Enum.GetValues<LocatorType>().Where(t => t != LocatorType.AccessibilityId).ToList();

    [ObservableProperty]
    private BrowserType selectedBrowser = BrowserType.Chrome;

    [ObservableProperty]
    private WebStepAction newAction = WebStepAction.Navigate;

    [ObservableProperty]
    private LocatorType newLocatorType = LocatorType.Id;

    [ObservableProperty]
    private string newLocator = string.Empty;

    [ObservableProperty]
    private LocatorType newTargetLocatorType = LocatorType.Id;

    [ObservableProperty]
    private string newTargetLocator = string.Empty;

    [ObservableProperty]
    private string newValue = string.Empty;

    [ObservableProperty]
    private int newTimeoutSeconds = 10;

    // Ha a lokátor több elemre is illik (pl. egy táblázat/lista minden sorában ugyanaz
    // az Id/Name/ClassName ismétlődik), ez adja meg, hányadik találattal dolgozzon a
    // lépés — 1-alapú, EMBERI számozás (1 = első elem) — üresen az első találat.
    [ObservableProperty]
    private string newElementIndex = "";

    // Hiba esetén ennyiszer próbálja újra a lépést — lásd TestStep.RetryCount.
    [ObservableProperty]
    private int newRetryCount;

    // "Self-healing" tartalék lokátor — lásd TestStep.FallbackLocator.
    [ObservableProperty]
    private string newFallbackLocator = "";

    [ObservableProperty]
    private LocatorType newFallbackLocatorType = LocatorType.Id;

    // Ha be van jelölve, a lépés hibája NEM szakítja meg a futtatást
    [ObservableProperty]
    private bool newContinueOnError;

    // Ha be van jelölve, a lépést a futtatás átugorja — meg sem kísérli végrehajtani.
    [ObservableProperty]
    private bool newSkip;

    // A lépés saját azonosító címkéje — automatikusan generált, felülírható.
    [ObservableProperty]
    private string newLabel = "";

    // Siker esetén ugrás célja (másik lépés Label-je) — üresen a normál, következő lépés jön.
    [ObservableProperty]
    private string? newOnSuccessGoToLabel;

    // Hiba esetén ugrás célja (másik lépés Label-je) — üresen a ContinueOnError dönt.
    [ObservableProperty]
    private string? newOnFailureGoToLabel;

    // A lépéslistában szereplő Label-ek + egy "— következő —" opció, ugrás-célpont választáshoz a ComboBox-okban.
    public IEnumerable<string> AvailableGoToLabels =>
        new[] { "" }.Concat(Steps.Select(s => s.Step.Label).Where(l => !string.IsNullOrWhiteSpace(l)));

    [ObservableProperty]
    private bool isRunning;

    /// <summary>Igaz, amíg a Felvevő mód aktív — a felület ez alapján mutatja/rejti a
    /// "Felvétel leállítása" gombot, és pirosít egy jelző-pöttyöt.</summary>
    [ObservableProperty]
    private bool isRecording;


    // A lépéslistában kijelölt sor — sorra kattintva állítódik be (lásd WebTestView.xaml,
    // SelectStepCommand). A billentyűparancsok (Delete, Ctrl+D, Ctrl+↑/↓) ezen keresztül
    // tudják, melyik lépésre vonatkozzanak.

    [ObservableProperty]
    private TestStepRow? selectedStep;

    [RelayCommand]
    private void SelectStep(TestStepRow? row) => SelectedStep = row;

    private TestStepRow? _editingRow;

    public bool IsEditing => _editingRow is not null;
    public string AddButtonLabel => IsEditing ? "Mentés" : "Hozzáadás";

    public bool IsLocatorNeeded => !NoLocatorActions.Contains(NewAction);
    public bool IsValueNeeded => !NoValueActions.Contains(NewAction);
    public bool IsTargetNeeded => NewAction == WebStepAction.DragAndDrop;

    private readonly int _defaultTimeoutSeconds;

    public WebTestViewModel(
        WebAutomationDriver driver,
        INotificationService notificationService,
        AT.Infrastructure.ISettingsService settingsService,
        AT.Infrastructure.ITestSuiteFileService fileService,
        ITestRunHistoryService historyService,
        ITestReportService reportService,
        IScheduledTaskService scheduledTaskService,
        ISchedulerService schedulerService,
        ITestCategoryService categoryService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
        _settingsService = settingsService;
        _historyService = historyService;
        _reportService = reportService;
        _scheduledTaskService = scheduledTaskService;
        _schedulerService = schedulerService;
        _categoryService = categoryService;
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _recordingTimer.Tick += async (_, _) => await PollRecordingAsync();

        Steps.CollectionChanged += (_, _) =>
        {
            RunStepsCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AvailableGoToLabels));
        };

        var defaults = settingsService.Current;
        _defaultTimeoutSeconds = defaults.DefaultTimeoutSeconds;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
        if (Enum.TryParse<BrowserType>(defaults.DefaultBrowser, ignoreCase: true, out var browser))
            SelectedBrowser = browser;

        LoadAvailableCategories();
    }

    partial void OnNewActionChanged(WebStepAction value)
    {
        OnPropertyChanged(nameof(IsLocatorNeeded));
        OnPropertyChanged(nameof(IsValueNeeded));
        OnPropertyChanged(nameof(IsTargetNeeded));
    }

    [RelayCommand]
    private void AddStep()
    {
        if (IsLocatorNeeded && string.IsNullOrWhiteSpace(NewLocator))
        {
            _notificationService.Show("A lokátor mező kötelező ehhez a lépéstípushoz.", NotificationType.Warning);
            return;
        }

        if (IsTargetNeeded && string.IsNullOrWhiteSpace(NewTargetLocator))
        {
            _notificationService.Show("Drag&Drop-hoz a cél-lokátor is kötelező.", NotificationType.Warning);
            return;
        }

        if (NewAction == WebStepAction.Navigate && string.IsNullOrWhiteSpace(NewValue))
        {
            _notificationService.Show("Navigate lépéshez add meg az URL-t az érték mezőben.", NotificationType.Warning);
            return;
        }

        var step = new TestStep
        {
            Id = _editingRow?.Step.Id ?? Guid.NewGuid().ToString("N"),
            Name = BuildStepName(NewAction, NewLocator, NewValue, NewTargetLocator),
            Target = AutomationTarget.Web,
            Action = NewAction.ToString(),
            Locator = NewLocator,
            LocatorType = NewLocatorType,
            TargetLocator = NewTargetLocator,
            TargetLocatorType = NewTargetLocatorType,
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

        NewAction = Enum.Parse<WebStepAction>(row.Step.Action);
        NewLocatorType = row.Step.LocatorType;
        NewLocator = row.Step.Locator ?? string.Empty;
        NewTargetLocatorType = row.Step.TargetLocatorType;
        NewTargetLocator = row.Step.TargetLocator ?? string.Empty;
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

        NewAction = WebStepAction.Navigate;
        NewLocator = string.Empty;
        NewTargetLocator = string.Empty;
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

    // "Új teszt" — teljesen letisztázza a nézetet: kiüríti a lépéslistát, a teszt
    // nevét, és megszakít egy esetleg folyamatban lévő szerkesztést. Ha már van
    // felvett lépés, előbb megerősítést kér.

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


    // Megnyitja az ütemezés-bekérő dialógust a jelenlegi lépéssorral és teszt-névvel.
    // Ha a felhasználó megerősíti, elmenti az ütemezett feladatot és újraszámítja a
    // scheduler számára a legközelebbi esedékességet.

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
            AutomationTarget.Web,
            Steps.Select(r => r.Step).ToList(),
            SelectedBrowser);

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

    // Egy lépés áthelyezése tetszőleges pozícióra — a drag&amp;drop átrendezéshez.
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


    // "Futtatás innentől" — a kijelölt lépéstől kezdve fut le a sor vége felé, a
    // megelőző lépéseket kihagyva. Hasznos hibakereséskor, ha nem szeretnéd az egész
    // sort újra lefuttatni egyetlen lépés ellenőrzéséhez.

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

        IsRunning = true;

        _schedulerService.SetModuleBusy(AutomationTarget.Web, true);

        RunStepsCommand.NotifyCanExecuteChanged();

        var startedAt = DateTime.Now;
        _currentRunScreenshotFolder = ResolveRunScreenshotFolder(startedAt);

        try
        {
            _driver.Browser = SelectedBrowser;
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

                var maxAttempts = Math.Max(1, row.Step.RetryCount + 1);
                var attempt = 0;
                string? lastErrorMessage = null;

                while (true)
                {
                    attempt++;
                    try
                    {
                        await _driver.ExecuteStepAsync(row.Step);
                        stopwatch.Stop();
                        row.Duration = stopwatch.Elapsed;
                        row.Status = TestStatus.Passed;
                        await CaptureScreenshotIfNeededAsync(row, isFailure: false);
                        wasSuccess = true;

                        if (attempt > 1)
                            _notificationService.Show($"{row.Step.Name} — sikerült a(z) {attempt}. próbálkozásra.", NotificationType.Info);

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
                        _notificationService.Show(
                            $"Lépés sikertelen: {row.Step.Name}" + (attempt > 1 ? $" ({attempt} próbálkozás után)" : ""),
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
            _notificationService.Show($"A böngésző indítása sikertelen: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsRunning = false;
            
            RunStepsCommand.NotifyCanExecuteChanged();

            _schedulerService.SetModuleBusy(AutomationTarget.Web, false);

            await SaveRunToHistoryAsync(startedAt, DateTime.Now);
        }
    }


    // Létrehozza (ha a Beállítások szerint egyáltalán készül kép) a futtatáshoz tartozó,
    // a teszt nevét és időbélyeget tartalmazó almappát. Null-t ad vissza, ha a screenshot
    // mód "Soha" — ilyenkor sem mappa, sem kép nem jön létre.

    private string? ResolveRunScreenshotFolder(DateTime startedAt)
    {
        if (_settingsService.Current.ScreenshotCaptureMode == AT.Infrastructure.ScreenshotCaptureMode.Never)
            return null;

        var baseFolder = string.IsNullOrWhiteSpace(_settingsService.Current.ScreenshotFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _settingsService.Current.ScreenshotFolderPath!;

        return ScreenshotFolderResolver.CreateRunFolder(baseFolder, TestName, startedAt);
    }

    // Összeállítja és elmenti a futtatás összegzését a közös history-tárolóba, majd riport-exportálhatóvá teszi.
    private async Task SaveRunToHistoryAsync(DateTime startedAt, DateTime finishedAt)
    {
        var record = new TestRunRecord
        {
            TestName = TestName,
            CategoryId = SelectedCategoryId,
            Target = AutomationTarget.Web,
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

    // A legutóbbi futtatás HTML riportjának exportálása fájlba, majd megnyitása böngészőben.
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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "web-riport.html" : $"{TestName}-riport.html"
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

    private bool CanRun() => !IsRunning && Steps.Count > 0 && !string.IsNullOrWhiteSpace(SelectedCategoryId);

    // A Beállításokban választott mód szerint (soha / csak hiba / minden lépés) ment képernyőképet,
    // a futtatáshoz tartozó, ResolveRunScreenshotFolder által létrehozott almappába.
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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "web-lepesek.xml" : $"{TestName}.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _fileService.SaveAsync(dialog.FileName, AutomationTarget.Web, Steps.Select(r => r.Step), TestName, SelectedCategoryId);
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
            var file = await _fileService.LoadAsync(dialog.FileName, AutomationTarget.Web);

            Steps.Clear();
            foreach (var dto in file.Steps)
                Steps.Add(new TestStepRow { Step = AT.Infrastructure.TestSuiteMapper.ToTestStep(dto, AutomationTarget.Web) });

            TestName = file.Name ?? "";

            // A fájlban mentett kategória csak akkor állítható be, ha még mindig létezik
            // és a Web platformon engedélyezett (lásd AvailableCategories) — enélkül egy
            // törölt vagy más gépen létrehozott kategória Id-ja "ragadna be" érvénytelenül.
            // LoadAvailableCategories() a jelenlegi, friss listát tölti be, majd ha a fájlból
            // betöltött CategoryId szerepel benne, azt választjuk; egyébként az alapértelmezett
            // (LoadAvailableCategories már beállított) első elérhető kategóriánál maradunk.

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

    /// <summary>
    /// Felvevő mód be-/kikapcsolása. Bekapcsoláskor — ugyanúgy, mint az Elem-kereső — ha
    /// nincs aktív session, CSATLAKOZUNK egy már megnyitott (vagy egyszeri alkalommal
    /// most elindított) böngészőhöz, majd beinjektáljuk a felvevő JS-t, és elindul egy
    /// időzítő, ami 600ms-enként lekérdezi az azóta történt kattintásokat/gépeléseket, és
    /// automatikusan lépésként hozzáadja őket a listához. Kikapcsoláskor csak az
    /// időzítőt állítjuk le és a JS-oldali figyelést kapcsoljuk ki — magát a böngésző-
    /// session-t NEM zárjuk be (ugyanúgy használható utána "Futtatás"-hoz is).
    /// </summary>
    [RelayCommand]
    private async Task ToggleRecording()
    {
        if (IsRecording)
        {
            _recordingTimer.Stop();
            IsRecording = false;

            try { await _driver.StopRecordingAsync(); }
            catch { /* a böngészőt úgyis rögtön bezárjuk, egy hiba itt nem kritikus */ }

            // A felvétel leállítása egy explicit, szándékos "végeztem" döntés — ilyenkor
            // a felvételhez megnyitott böngészőnek is be kell záródnia, ne kelljen még
            // külön a "Böngésző bezárása" gombra is kattintani.
            await _driver.CloseBrowserForceAsync();

            _notificationService.Show("Felvétel leállítva, böngésző bezárva.", NotificationType.Info);
            return;
        }

        if (!_driver.IsRunning)
        {
            // IsRecording AZONNAL true-ra áll, MIELŐTT bármilyen await elindulna — enélkül,
            // ha a böngésző megnyitása pár másodpercig tart, és a felhasználó türelmetlenül
            // újra megnyomja a Ctrl+R-t/gombot, a második hívás is IsRecording==false-t
            // látna, és megint az "indítás" ágba futna (versenyhelyzet: második böngésző
            // nyílna, vagy a felvétel dupla indulna). Mivel egy async metódus teste az
            // első await-ig SZINKRON fut le, ez a sor garantáltan lefut, mielőtt bármi
            // más (pl. egy második gombnyomás) a metódusba léphetne.
            IsRecording = true;

            try
            {
                await _driver.AttachToRunningBrowserAsync();
            }
            catch (Exception ex)
            {
                IsRecording = false;
                _notificationService.Show($"Nem sikerült csatlakozni a böngészőhöz: {ex.Message}", NotificationType.Error);
                return;
            }
        }
        else
        {
            IsRecording = true;
        }

        try
        {
            await _driver.StartRecordingAsync();
        }
        catch (Exception ex)
        {
            IsRecording = false;
            _notificationService.Show($"Felvétel indítása sikertelen: {ex.Message}", NotificationType.Error);
            return;
        }

        _recordingTimer.Start();
        _notificationService.Show(
            "Felvétel elindult — böngéssz és kattints/gépelj normálisan a böngészőben, a lépések automatikusan megjelennek a listában.",
            NotificationType.Success);
    }

    /// <summary>A _recordingTimer hívja rendszeresen, amíg a felvétel aktív. Minden
    /// lekérdezett eseményből egy teljes TestStep-et épít, és hozzáadja a Steps-hez.
    /// Ha a felhasználó közben új oldalra navigált (a JS-kontextus emiatt törlődött),
    /// csendben újra beinjektáljuk a figyelőt — enélkül navigáció után a felvétel
    /// némán leállna, amit a felhasználó könnyen észre sem venne.</summary>
    private async Task PollRecordingAsync()
    {
        List<RecordedWebAction> actions;
        try
        {
            actions = await _driver.PollRecordedActionsAsync();
        }
        catch
        {
            return;
        }

        // Ha a queue üres ÉS a session még fut, megpróbáljuk újra beinjektálni a
        // figyelőt is — ez olcsó (a JS elején van egy "már fut" ellenőrzés), és
        // biztosítja, hogy egy navigáció utáni új oldalon is folytatódjon a felvétel.
        if (_driver.IsRunning)
        {
            try { await _driver.StartRecordingAsync(); }
            catch { /* ha épp navigáció közben vagyunk, a következő tick úgyis újrapróbálja */ }
        }

        foreach (var recorded in actions)
        {
            if (!Enum.TryParse<LocatorType>(recorded.LocatorType, out var locatorType))
                locatorType = LocatorType.XPath;

            if (!Enum.TryParse<WebStepAction>(recorded.Action, out var action))
                continue;

            var step = new TestStep
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = BuildStepName(action, recorded.Locator, recorded.Value ?? "", recorded.TargetLocator ?? ""),
                Target = AutomationTarget.Web,
                Action = action.ToString(),
                Locator = recorded.Locator,
                LocatorType = locatorType,
                Value = recorded.Value,
                TimeoutSeconds = _defaultTimeoutSeconds,
                Label = AT.Infrastructure.StepFlowResolver.GenerateNextLabel(Steps.Select(r => r.Step).ToList())
            };

            // A DragAndDrop akciónál a JS a cél-lokátort is elküldi (lásd
            // RecorderAttachScript "drop" eseménykezelője) — ezt a TestStep saját
            // TargetLocator/TargetLocatorType mezőire kell átvenni, mert a futtatáskori
            // ExecuteStepCore ebből olvassa ki a húzás célpontját, nem a Locator-ból.
            if (action == WebStepAction.DragAndDrop && !string.IsNullOrWhiteSpace(recorded.TargetLocator))
            {
                if (!Enum.TryParse<LocatorType>(recorded.TargetLocatorType, out var targetLocatorType))
                    targetLocatorType = LocatorType.XPath;

                step.TargetLocator = recorded.TargetLocator;
                step.TargetLocatorType = targetLocatorType;
            }

            Steps.Add(new TestStepRow { Step = step });
        }
    }

    [RelayCommand]
    private async Task OpenElementFinderAsync()
    {
        // Ha már fut egy session (akár egy korábbi Futtatás-tól, akár egy korábbi
        // Elem-kereső-megnyitástól), egyszerűen azt használjuk — nem indítunk/
        // csatlakozunk semmi újhoz. Ha nincs aktív session, CSATLAKOZUNK egy már
        // megnyitott (vagy egyszeri alkalommal, most elindított) böngészőhöz,
        // ahelyett hogy egy külön, kizárólag automatizálásra szánt új példányt
        // nyitnánk — lásd AttachToRunningBrowserAsync dokumentációját.
        if (!_driver.IsRunning)
        {
            try
            {
                await _driver.AttachToRunningBrowserAsync();
            }
            catch (Exception ex)
            {
                _notificationService.Show($"Nem sikerült csatlakozni a böngészőhöz: {ex.Message}", NotificationType.Error);
                return;
            }
        }

        var window = new AT.App.Views.InspectorWindow(null, _driver, AT.App.ViewModels.InspectorPlatform.Web, (type, value, matchIndex) =>
        {
            NewLocatorType = type;
            NewLocator = value;
            NewElementIndex = matchIndex?.ToString() ?? "";

            var indexNote = matchIndex.HasValue ? $" (elem sorszáma: {matchIndex})" : "";
            _notificationService.Show($"Lokátor beillesztve az elem-keresőből{indexNote}.", NotificationType.Success);
        });

        window.Show();
    }

    [RelayCommand]
    private async Task CloseBrowserAsync()
    {
        // Ha épp fut a Felvevő, azt is le kell állítani — a böngésző bezárása után a
        // JS-oldali figyelő úgyis eltűnik, az időzítő viszont felesleges hívásokat
        // küldene egy már nem létező session-nek.
        if (IsRecording)
        {
            _recordingTimer.Stop();
            IsRecording = false;
        }

        // Szándékosan a "force" változatot hívjuk, NEM a StopAsync-et — ez a gomb egy
        // explicit, szándékos "zárd be a böngészőt" döntés a felhasználótól, ezért
        // mindig ténylegesen be kell záródnia, akkor is, ha épp egy már korábban futó
        // (nem az AT Studio által indított) böngészőhöz csatlakoztunk.
        await _driver.CloseBrowserForceAsync();
        _notificationService.Show("Böngésző bezárva.", NotificationType.Info);
    }

    // ===================== BILLENTYŰPARANCSOK =====================
    // A WebTestView.xaml.cs PreviewKeyDown-ja hívja meg ezeket, a SelectedStep-et
    // használva "a kijelölt lépés" gyanánt. A metódusok szándékosan tolerálják a
    // hiányzó kijelölést (null SelectedStep esetén egyszerűen nem csinálnak semmit).

    // Ctrl+S — a kijelölt lépéstől függetlenül mindig a teljes lépéssort menti.
    public void HandleSaveShortcut() => SaveStepsCommand.Execute(null);

    // Ctrl+O — lépéssor betöltése.
    public void HandleLoadShortcut() => LoadStepsCommand.Execute(null);

    // F5 — teljes futtatás az elejétől.
    public void HandleRunShortcut()
    {
        if (RunStepsCommand.CanExecute(null))
            RunStepsCommand.Execute(null);
    }

    // Shift+F5 — leállítás (a Web modulban ez a böngésző bezárását jelenti).
    public void HandleStopShortcut() => CloseBrowserCommand.Execute(null);

    // Delete — a kijelölt lépés törlése
    public void HandleDeleteShortcut()
    {
        if (SelectedStep is { } row)
            RemoveStepCommand.Execute(row);
    }

    // Ctrl+D — a kijelölt lépés duplikálása.
    public void HandleDuplicateShortcut()
    {
        if (SelectedStep is { } row)
            DuplicateStepCommand.Execute(row);
    }

    // Ctrl+↑ — a kijelölt lépés feljebb mozgatása.
    public void HandleMoveUpShortcut()
    {
        if (SelectedStep is { } row)
            MoveStepUpCommand.Execute(row);
    }

    // Ctrl+↓ — a kijelölt lépés lejjebb mozgatása.
    public void HandleMoveDownShortcut()
    {
        if (SelectedStep is { } row)
            MoveStepDownCommand.Execute(row);
    }

    // Esc — folyamatban lévő szerkesztés megszakítása.
    public void HandleEscapeShortcut()
    {
        if (IsEditing)
            CancelEditCommand.Execute(null);
    }

    private static string BuildStepName(WebStepAction action, string locator, string value, string targetLocator) => action switch
    {
        WebStepAction.Navigate => $"Navigálás → {value}",
        WebStepAction.Click => $"Kattintás → {locator}",
        WebStepAction.DoubleClick => $"Dupla kattintás → {locator}",
        WebStepAction.RightClick => $"Jobb-klikk → {locator}",
        WebStepAction.SendKeys => $"Szöveg beírás → {locator} → {value}",
        WebStepAction.Clear => $"Mező ürítése → {locator}",
        WebStepAction.Hover => $"Rámutatás → {locator}",
        WebStepAction.SelectByText => $"Kiválasztás szöveg alapján → {locator} → {value}",
        WebStepAction.SelectByValue => $"Kiválasztás érték alapján → {locator} → {value}",
        WebStepAction.DragAndDrop => $"Drag and Drop → {locator} ⇒ {targetLocator}",
        WebStepAction.Wait => "Várakozás",
        WebStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        WebStepAction.WaitClickable => $"Várakozás kattinthatóra → {locator}",
        WebStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        WebStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        WebStepAction.WaitHasText => $"Várakozás szövegre → {locator} → {value}",
        WebStepAction.WaitHasAttribute => $"Várakozás attribútumra → {locator} → {value}",
        WebStepAction.WaitHasClass => $"Várakozás class-ra → {locator} → {value}",
        WebStepAction.WaitHasValue => $"Várakozás value-ra → {locator} → {value}",
        WebStepAction.WaitHasCssValue => $"Várakozás CSS-re → {locator} → {value}",
        WebStepAction.WaitHasStyle => $"Várakozás style-ra → {locator} → {value}",
        _ => action.ToString()
    };
}
