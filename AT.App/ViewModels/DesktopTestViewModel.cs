using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Desktop;
using AT.Core.Contracts;
using AT.Core.Models;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class DesktopTestViewModel : ObservableObject
{
    private readonly DesktopAutomationDriver _driver;
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

    private static readonly DesktopStepAction[] NoLocatorActions =
        { DesktopStepAction.LaunchApp, DesktopStepAction.AttachToWindow, DesktopStepAction.Wait, DesktopStepAction.Close };

    private static readonly DesktopStepAction[] NoValueActions =
    {
        DesktopStepAction.Click, DesktopStepAction.DoubleClick, DesktopStepAction.RightClick,
        DesktopStepAction.Hover, DesktopStepAction.Clear, DesktopStepAction.DragAndDrop,
        DesktopStepAction.WaitVisible, DesktopStepAction.WaitEnabled, DesktopStepAction.WaitClickable,
        DesktopStepAction.WaitPresent, DesktopStepAction.WaitAbsent, DesktopStepAction.WaitSelected,
        DesktopStepAction.Wait, DesktopStepAction.Close
    };

    private static readonly LocatorType[] SupportedLocatorTypes =
        { LocatorType.Id, LocatorType.Name, LocatorType.ClassName, LocatorType.XPath };

    public string Title => "Windows desktop tesztelés";
    public string Description => "";

    public ObservableCollection<TestStepRow> Steps { get; } = new();

    [ObservableProperty]
    private string testName = "";

    public IReadOnlyList<DesktopStepAction> AvailableActions { get; } = Enum.GetValues<DesktopStepAction>();
    public IReadOnlyList<LocatorType> AvailableLocatorTypes { get; } = SupportedLocatorTypes;

    [ObservableProperty]
    private DesktopStepAction newAction = DesktopStepAction.LaunchApp;

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

    [ObservableProperty]
    private bool isRunning;

    /// <summary>
    /// A lépéslistában kijelölt sor — sorra kattintva állítódik be (lásd DesktopTestView.xaml,
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
    public bool IsTargetNeeded => NewAction == DesktopStepAction.DragAndDrop;

    private readonly int _defaultTimeoutSeconds;
    private readonly string? _defaultAppPath;

    public DesktopTestViewModel(
        DesktopAutomationDriver driver,
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
        Steps.CollectionChanged += (_, _) => RunStepsCommand.NotifyCanExecuteChanged();

        var defaults = settingsService.Current;
        _defaultTimeoutSeconds = defaults.DefaultTimeoutSeconds;
        _defaultAppPath = defaults.DefaultDesktopAppPath;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
    }

    partial void OnNewActionChanged(DesktopStepAction value)
    {
        OnPropertyChanged(nameof(IsLocatorNeeded));
        OnPropertyChanged(nameof(IsValueNeeded));
        OnPropertyChanged(nameof(IsTargetNeeded));

        if (value == DesktopStepAction.LaunchApp && string.IsNullOrWhiteSpace(NewValue) && !string.IsNullOrWhiteSpace(_defaultAppPath))
            NewValue = _defaultAppPath;
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

        if ((NewAction == DesktopStepAction.LaunchApp || NewAction == DesktopStepAction.AttachToWindow)
            && string.IsNullOrWhiteSpace(NewValue))
        {
            _notificationService.Show("Add meg az .exe elérési útját vagy az ablak címét az érték mezőben.", NotificationType.Warning);
            return;
        }

        var step = new TestStep
        {
            Id = _editingRow?.Step.Id ?? Guid.NewGuid().ToString("N"),
            Name = BuildStepName(NewAction, NewLocator, NewValue, NewTargetLocator),
            Target = AutomationTarget.Desktop,
            Action = NewAction.ToString(),
            Locator = NewLocator,
            LocatorType = NewLocatorType,
            TargetLocator = NewTargetLocator,
            TargetLocatorType = NewTargetLocatorType,
            Value = NewValue,
            TimeoutSeconds = NewTimeoutSeconds,
            ContinueOnError = NewContinueOnError,
            Skip = NewSkip
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

        NewAction = Enum.Parse<DesktopStepAction>(row.Step.Action);
        NewLocatorType = row.Step.LocatorType;
        NewLocator = row.Step.Locator ?? string.Empty;
        NewTargetLocatorType = row.Step.TargetLocatorType;
        NewTargetLocator = row.Step.TargetLocator ?? string.Empty;
        NewValue = row.Step.Value ?? string.Empty;
        NewTimeoutSeconds = row.Step.TimeoutSeconds;
        NewContinueOnError = row.Step.ContinueOnError;
        NewSkip = row.Step.Skip;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddButtonLabel));
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingRow = null;

        NewAction = DesktopStepAction.LaunchApp;
        NewLocator = string.Empty;
        NewTargetLocator = string.Empty;
        NewValue = string.Empty;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
        NewContinueOnError = false;
        NewSkip = false;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddButtonLabel));
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
            await _driver.StartAsync();

            for (var i = startIndex; i < Steps.Count; i++)
            {
                var row = Steps[i];

                row.Message = null;
                row.Duration = null;
                row.ScreenshotPath = null;

                if (row.Step.Skip)
                {
                    row.Status = TestStatus.Skipped;
                    continue;
                }

                row.Status = TestStatus.Running;
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await _driver.ExecuteStepAsync(row.Step);
                    stopwatch.Stop();
                    row.Duration = stopwatch.Elapsed;
                    row.Status = TestStatus.Passed;
                    await CaptureScreenshotIfNeededAsync(row, isFailure: false);

                    if (!string.IsNullOrEmpty(result))
                        _notificationService.Show($"{row.Step.Name} → {result}", NotificationType.Info);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    row.Duration = stopwatch.Elapsed;
                    row.Status = TestStatus.Failed;
                    row.Message = ex.Message;
                    _notificationService.Show($"Lépés sikertelen: {row.Step.Name}", NotificationType.Error);
                    await CaptureScreenshotIfNeededAsync(row, isFailure: true);

                    if (!row.Step.ContinueOnError)
                        break;
                }
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
            Target = AutomationTarget.Desktop,
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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "desktop-riport.html" : $"{TestName}-riport.html"
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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "desktop-lepesek.xml" : $"{TestName}.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _fileService.SaveAsync(dialog.FileName, AutomationTarget.Desktop, Steps.Select(r => r.Step), TestName);
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
            var file = await _fileService.LoadAsync(dialog.FileName, AutomationTarget.Desktop);

            Steps.Clear();
            foreach (var dto in file.Steps)
                Steps.Add(new TestStepRow { Step = AT.Infrastructure.TestSuiteMapper.ToTestStep(dto, AutomationTarget.Desktop) });

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

        var window = new AT.App.Views.InspectorWindow(_driver, null, AT.App.ViewModels.InspectorPlatform.Desktop, (type, value) =>
        {
            NewLocatorType = type;
            NewLocator = value;
            _notificationService.Show("Lokátor beillesztve az elem-keresőből.", NotificationType.Success);
        });

        window.Show();
    }

    [RelayCommand]
    private async Task CloseAppAsync()
    {
        await _driver.StopAsync();
        _notificationService.Show("Alkalmazás bezárva / leválasztva.", NotificationType.Info);
    }

    // ===================== BILLENTYŰPARANCSOK =====================
    // A DesktopTestView.xaml.cs PreviewKeyDown-ja hívja meg ezeket, a SelectedStep-et
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

    /// <summary>Shift+F5 — leállítás (a Desktop modulban ez az alkalmazás bezárását jelenti).</summary>
    public void HandleStopShortcut() => CloseAppCommand.Execute(null);

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

    private static string BuildStepName(DesktopStepAction action, string locator, string value, string targetLocator) => action switch
    {
        DesktopStepAction.LaunchApp => $"Alkalmazás indítása → {value}",
        DesktopStepAction.AttachToWindow => $"Csatlakozás ablakhoz → {value}",
        DesktopStepAction.Click => $"Kattintás → {locator}",
        DesktopStepAction.DoubleClick => $"Dupla kattintás → {locator}",
        DesktopStepAction.RightClick => $"Jobb-klikk → {locator}",
        DesktopStepAction.SetText => $"Szöveg beállítása → {locator} → {value}",
        DesktopStepAction.Clear => $"Mező ürítése → {locator}",
        DesktopStepAction.Hover => $"Rámutatás → {locator}",
        DesktopStepAction.SelectComboBoxItem => $"Lista-elem kiválasztása → {locator} → {value}",
        DesktopStepAction.DragAndDrop => $"Húzás → {locator} ⇒ {targetLocator}",
        DesktopStepAction.ReadAttribute => $"Attribútum kiolvasása → {locator} → {value}",
        DesktopStepAction.Wait => "Várakozás",
        DesktopStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        DesktopStepAction.WaitEnabled => $"Várakozás elérhetőre → {locator}",
        DesktopStepAction.WaitClickable => $"Várakozás kattinthatóra → {locator}",
        DesktopStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        DesktopStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        DesktopStepAction.WaitSelected => $"Várakozás kiválasztásra → {locator}",
        DesktopStepAction.WaitHasText => $"Várakozás szövegre → {locator} → {value}",
        DesktopStepAction.WaitHasValue => $"Várakozás értékre → {locator} → {value}",
        DesktopStepAction.WaitHasClass => $"Várakozás class-ra → {locator} → {value}",
        DesktopStepAction.WaitHasAttribute => $"Várakozás attribútumra → {locator} → {value}",
        DesktopStepAction.Close => "Alkalmazás bezárása",
        _ => action.ToString()
    };
}
