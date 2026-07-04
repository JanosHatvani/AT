using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private bool isRunning;

    private TestStepRow? _editingRow;

    [ObservableProperty]
    private string testName = "";

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
            TimeoutSeconds = NewTimeoutSeconds
        };

        if (_editingRow is not null)
        {
            _editingRow.Step = step;
            _editingRow.Status = TestStatus.NotRun;
            _editingRow.Message = null;
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
                row.Status = TestStatus.Running;
                row.Message = null;

                try
                {
                    await _driver.ExecuteStepAsync(row.Step);
                    row.Status = TestStatus.Passed;
                }
                catch (Exception ex)
                {
                    row.Status = TestStatus.Failed;
                    row.Message = ex.Message;
                    _notificationService.Show($"Lépés sikertelen: {row.Step.Name}", NotificationType.Error);
                    break;
                }
            }

            var hasFailed = Steps.Any(s => s.Status == TestStatus.Failed);
            _notificationService.Show(
                hasFailed ? "A futtatás hibával leállt." : "Minden lépés sikeresen lefutott.",
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
        WebStepAction.SendKeys => $"Beírás ({locator}) → {value}",
        WebStepAction.Clear => $"Mező ürítése → {locator}",
        WebStepAction.Hover => $"Rámutatás → {locator}",
        WebStepAction.SelectByText => $"Kiválasztás szöveg alapján ({locator}) → {value}",
        WebStepAction.SelectByValue => $"Kiválasztás érték alapján ({locator}) → {value}",
        WebStepAction.DragAndDrop => $"Húzás → {locator} ⇒ {targetLocator}",
        WebStepAction.Wait => "Várakozás",
        WebStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        WebStepAction.WaitClickable => $"Várakozás kattinthatóra → {locator}",
        WebStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        WebStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        WebStepAction.WaitHasText => $"Várakozás szövegre ({locator}) → {value}",
        WebStepAction.WaitHasAttribute => $"Várakozás attribútumra ({locator}) → {value}",
        WebStepAction.WaitHasClass => $"Várakozás class-ra ({locator}) → {value}",
        WebStepAction.WaitHasValue => $"Várakozás value-ra ({locator}) → {value}",
        WebStepAction.WaitHasCssValue => $"Várakozás CSS-re ({locator}) → {value}",
        WebStepAction.WaitHasStyle => $"Várakozás style-ra ({locator}) → {value}",
        _ => action.ToString()
    };
}