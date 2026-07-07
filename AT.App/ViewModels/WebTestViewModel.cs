using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Web;
using AT.Core.Contracts;
using AT.Core.Models;
using AT.Infrastructure;
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

    /// <summary>A folyamatban lévő (vagy legutóbb befejezett) futtatás képernyőkép-mappája — null, ha ehhez a futtatáshoz nem készül kép.</summary>
    private string? _currentRunScreenshotFolder;

    /// <summary>A legutóbbi futtatás összegzése — a "Riport exportálása" gomb ezt írja ki HTML-be.</summary>
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

    /// <summary>
    /// A lépéslistában kijelölt sor — sorra kattintva állítódik be (lásd WebTestView.xaml,
    /// SelectStepCommand). A billentyűparancsok (Delete, Ctrl+D, Ctrl+↑/↓) ezen keresztül
    /// tudják, melyik lépésre vonatkozzanak.
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
    public bool IsTargetNeeded => NewAction == WebStepAction.DragAndDrop;

    private readonly int _defaultTimeoutSeconds;

    public WebTestViewModel(
        WebAutomationDriver driver,
        INotificationService notificationService,
        AT.Infrastructure.ISettingsService settingsService,
        AT.Infrastructure.ITestSuiteFileService fileService,
        ITestRunHistoryService historyService,
        ITestReportService reportService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
        _settingsService = settingsService;
        _historyService = historyService;
        _reportService = reportService;
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
    /// nevét, és megszakít egy esetleg folyamatban lévő szerkesztést. Ha már van
    /// felvett lépés, előbb megerősítést kér.
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
        _notificationService.Show("Új, üres lépéssor létrehozva.", NotificationType.Info);
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
    /// sort újra lefuttatni egyetlen lépés ellenőrzéséhez.
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
        IsRunning = true;
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

                try
                {
                    await _driver.ExecuteStepAsync(row.Step);
                    stopwatch.Stop();
                    row.Duration = stopwatch.Elapsed;
                    row.Status = TestStatus.Passed;
                    await CaptureScreenshotIfNeededAsync(row, isFailure: false);
                    wasSuccess = true;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    row.Duration = stopwatch.Elapsed;
                    row.Status = TestStatus.Failed;
                    row.Message = ex.Message;
                    _notificationService.Show($"Lépés sikertelen: {row.Step.Name}", NotificationType.Error);
                    await CaptureScreenshotIfNeededAsync(row, isFailure: true);
                    wasSuccess = false;
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

    private bool CanRun() => !IsRunning && Steps.Count > 0;

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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "web-lepesek.xml" : $"{TestName}.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _fileService.SaveAsync(dialog.FileName, AutomationTarget.Web, Steps.Select(r => r.Step), TestName);
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
            CancelEdit();
            _notificationService.Show($"{file.Steps.Count} lépés betöltve.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Betöltés sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task OpenElementFinderAsync()
    {
        await _driver.StartAsync();

        var window = new AT.App.Views.InspectorWindow(null, _driver, AT.App.ViewModels.InspectorPlatform.Web, (type, value) =>
        {
            NewLocatorType = type;
            NewLocator = value;
            _notificationService.Show("Lokátor beillesztve az elem-keresőből.", NotificationType.Success);
        });

        window.Show();
    }

    [RelayCommand]
    private async Task CloseBrowserAsync()
    {
        await _driver.StopAsync();
        _notificationService.Show("Böngésző bezárva.", NotificationType.Info);
    }

    // ===================== BILLENTYŰPARANCSOK =====================
    // A WebTestView.xaml.cs PreviewKeyDown-ja hívja meg ezeket, a SelectedStep-et
    // használva "a kijelölt lépés" gyanánt. A metódusok szándékosan tolerálják a
    // hiányzó kijelölést (null SelectedStep esetén egyszerűen nem csinálnak semmit).

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

    /// <summary>Shift+F5 — leállítás (a Web modulban ez a böngésző bezárását jelenti).</summary>
    public void HandleStopShortcut() => CloseBrowserCommand.Execute(null);

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

    private static string BuildStepName(WebStepAction action, string locator, string value, string targetLocator) => action switch
    {
        WebStepAction.Navigate => $"Navigálás → {value}",
        WebStepAction.Click => $"Kattintás → {locator}",
        WebStepAction.DoubleClick => $"Dupla kattintás → {locator}",
        WebStepAction.RightClick => $"Jobb-klikk → {locator}",
        WebStepAction.SendKeys => $"Beírás → {locator} → {value}",
        WebStepAction.Clear => $"Mező ürítése → {locator}",
        WebStepAction.Hover => $"Rámutatás → {locator}",
        WebStepAction.SelectByText => $"Kiválasztás szöveg alapján → {locator} → {value}",
        WebStepAction.SelectByValue => $"Kiválasztás érték alapján → {locator} → {value}",
        WebStepAction.DragAndDrop => $"Húzás → {locator} ⇒ {targetLocator}",
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
