using System.Collections.ObjectModel;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Desktop;
using AT.Core.Contracts;
using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class DesktopTestViewModel : ObservableObject
{
    private readonly DesktopAutomationDriver _driver;
    private readonly INotificationService _notificationService;
    private readonly AT.Infrastructure.ITestSuiteFileService _fileService;

    [ObservableProperty]
    private string testName = "";

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
    public string Description => "FlaUI (UIA3) alapú lépéslista — a Winium leváltása.";

    public ObservableCollection<TestStepRow> Steps { get; } = new();

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

    [ObservableProperty]
    private bool isRunning;

    private TestStepRow? _editingRow;

    public bool IsEditing => _editingRow is not null;
    public string AddButtonLabel => IsEditing ? "Mentés" : "Hozzáadás";

    public bool IsLocatorNeeded => !NoLocatorActions.Contains(NewAction);
    public bool IsValueNeeded => !NoValueActions.Contains(NewAction);
    public bool IsTargetNeeded => NewAction == DesktopStepAction.DragAndDrop;

    private readonly int _defaultTimeoutSeconds;
    private readonly string? _defaultAppPath;

    public DesktopTestViewModel(DesktopAutomationDriver driver, INotificationService notificationService, AT.Infrastructure.ISettingsService settingsService, AT.Infrastructure.ITestSuiteFileService fileService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
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

        NewAction = Enum.Parse<DesktopStepAction>(row.Step.Action);
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

        NewAction = DesktopStepAction.LaunchApp;
        NewLocator = string.Empty;
        NewTargetLocator = string.Empty;
        NewValue = string.Empty;
        NewTimeoutSeconds = 10;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(AddButtonLabel));
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
            await _driver.StartAsync();

            foreach (var row in Steps)
            {
                row.Status = TestStatus.Running;
                row.Message = null;

                try
                {
                    var result = await _driver.ExecuteStepAsync(row.Step);
                    row.Status = TestStatus.Passed;

                    if (!string.IsNullOrEmpty(result))
                        _notificationService.Show($"{row.Step.Name} → {result}", NotificationType.Info);
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
            _notificationService.Show($"Hiba a futtatás közben: {ex.Message}", NotificationType.Error);
        }
        finally
        {
            IsRunning = false;
            RunStepsCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRun() => !IsRunning && Steps.Count > 0;


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

        

    private static string BuildStepName(DesktopStepAction action, string locator, string value, string targetLocator) => action switch
    {
        DesktopStepAction.LaunchApp => $"Alkalmazás indítása → {value}",
        DesktopStepAction.AttachToWindow => $"Csatlakozás ablakhoz → {value}",
        DesktopStepAction.Click => $"Kattintás → {locator}",
        DesktopStepAction.DoubleClick => $"Dupla kattintás → {locator}",
        DesktopStepAction.RightClick => $"Jobb-klikk → {locator}",
        DesktopStepAction.SetText => $"Szöveg beállítása ({locator}) → {value}",
        DesktopStepAction.Clear => $"Mező ürítése → {locator}",
        DesktopStepAction.Hover => $"Rámutatás → {locator}",
        DesktopStepAction.SelectComboBoxItem => $"Lista-elem kiválasztása ({locator}) → {value}",
        DesktopStepAction.DragAndDrop => $"Húzás → {locator} ⇒ {targetLocator}",
        DesktopStepAction.ReadAttribute => $"Attribútum kiolvasása ({locator}) → {value}",
        DesktopStepAction.Wait => "Várakozás",
        DesktopStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        DesktopStepAction.WaitEnabled => $"Várakozás elérhetőre → {locator}",
        DesktopStepAction.WaitClickable => $"Várakozás kattinthatóra → {locator}",
        DesktopStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        DesktopStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        DesktopStepAction.WaitSelected => $"Várakozás kiválasztásra → {locator}",
        DesktopStepAction.WaitHasText => $"Várakozás szövegre ({locator}) → {value}",
        DesktopStepAction.WaitHasValue => $"Várakozás értékre ({locator}) → {value}",
        DesktopStepAction.WaitHasClass => $"Várakozás class-ra ({locator}) → {value}",
        DesktopStepAction.WaitHasAttribute => $"Várakozás attribútumra ({locator}) → {value}",
        DesktopStepAction.Close => "Alkalmazás bezárása",
        _ => action.ToString()
    };
}