using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Web;
using AT.Core.Contracts;
using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class WebTestViewModel : ObservableObject
{
    private readonly WebAutomationDriver _driver;
    private readonly INotificationService _notificationService;
    private readonly AT.Infrastructure.ITestSuiteFileService _fileService;
    private readonly AT.Infrastructure.ISettingsService _settingsService;

    private static readonly WebStepAction[] NoLocatorActions = { WebStepAction.Navigate, WebStepAction.Wait };
    private static readonly WebStepAction[] NoValueActions =
    {
        WebStepAction.Click, WebStepAction.DoubleClick, WebStepAction.RightClick, WebStepAction.Hover,
        WebStepAction.Clear, WebStepAction.WaitVisible, WebStepAction.WaitClickable,
        WebStepAction.WaitPresent, WebStepAction.WaitAbsent, WebStepAction.DragAndDrop
    };

    public string Title => "Web tesztelés";
    public string Description => "Selenium alapú lépéslista — Chrome, Firefox vagy Edge.";

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

    [ObservableProperty]
    private bool isRunning;

    private TestStepRow? _editingRow;

    public bool IsEditing => _editingRow is not null;
    public string AddButtonLabel => IsEditing ? "Mentés" : "Hozzáadás";

    public bool IsLocatorNeeded => !NoLocatorActions.Contains(NewAction);
    public bool IsValueNeeded => !NoValueActions.Contains(NewAction);
    public bool IsTargetNeeded => NewAction == WebStepAction.DragAndDrop;

    private readonly int _defaultTimeoutSeconds;

    public WebTestViewModel(WebAutomationDriver driver, INotificationService notificationService, AT.Infrastructure.ISettingsService settingsService, AT.Infrastructure.ITestSuiteFileService fileService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
        _settingsService = settingsService;
        Steps.CollectionChanged += (_, _) => RunStepsCommand.NotifyCanExecuteChanged();

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

        NewAction = Enum.Parse<WebStepAction>(row.Step.Action);
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

        NewAction = WebStepAction.Navigate;
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
    private async Task RunStepsAsync()
    {
        IsRunning = true;
        RunStepsCommand.NotifyCanExecuteChanged();

        try
        {
            _driver.Browser = SelectedBrowser;
            await _driver.StartAsync();

            foreach (var row in Steps)
            {
                row.Message = null;
                row.Duration = null;

                if (row.Step.Skip)
                {
                    row.Status = TestStatus.Skipped;
                    continue;
                }

                row.Status = TestStatus.Running;
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await _driver.ExecuteStepAsync(row.Step);
                    stopwatch.Stop();
                    row.Duration = stopwatch.Elapsed;
                    row.Status = TestStatus.Passed;
                    await CaptureScreenshotIfNeededAsync(row, isFailure: false);
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
            _notificationService.Show($"A böngésző indítása sikertelen: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsRunning = false;
            RunStepsCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRun() => !IsRunning && Steps.Count > 0;

    /// <summary>A Beállításokban választott mód szerint (soha / csak hiba / minden lépés) ment képernyőképet.</summary>
    private async Task CaptureScreenshotIfNeededAsync(TestStepRow row, bool isFailure)
    {
        var mode = _settingsService.Current.ScreenshotCaptureMode;
        var shouldCapture = mode == AT.Infrastructure.ScreenshotCaptureMode.Always
            || (isFailure && mode == AT.Infrastructure.ScreenshotCaptureMode.OnErrorOnly);

        if (!shouldCapture)
            return;

        try
        {
            var bytes = await _driver.GetScreenshotAsync();

            var folder = string.IsNullOrWhiteSpace(_settingsService.Current.ScreenshotFolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : _settingsService.Current.ScreenshotFolderPath!;

            Directory.CreateDirectory(folder);

            var fileName = $"web_{SanitizeFileName(row.Step.Name)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            var fullPath = Path.Combine(folder, fileName);

            await File.WriteAllBytesAsync(fullPath, bytes);

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