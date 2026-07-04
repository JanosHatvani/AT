using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AT.App.Models;
using AT.App.Services;
using AT.Automation.Mobile;
using AT.Core.Contracts;
using AT.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

public sealed partial class MobileTestViewModel : ObservableObject
{
    private readonly MobileAutomationDriver _driver;
    private readonly INotificationService _notificationService;
    private readonly DispatcherTimer _mirrorTimer;

    private readonly int _defaultTimeoutSeconds;
    private readonly string? _defaultAvdName;
    private readonly string? _defaultApkPath;

    private readonly AT.Infrastructure.ITestSuiteFileService _fileService;

    private static readonly MobileStepAction[] NoLocatorActions =
    {
        MobileStepAction.StartEmulator, MobileStepAction.LaunchApp, MobileStepAction.Swipe,
        MobileStepAction.Wait, MobileStepAction.Close, MobileStepAction.StopEmulator
    };

    private static readonly MobileStepAction[] NoValueActions =
    {
        MobileStepAction.Click, MobileStepAction.LongPress, MobileStepAction.Clear,
        MobileStepAction.ScrollToElement, MobileStepAction.WaitVisible, MobileStepAction.WaitPresent,
        MobileStepAction.WaitAbsent, MobileStepAction.Wait, MobileStepAction.Close, MobileStepAction.StopEmulator
    };

    private static readonly LocatorType[] SupportedLocatorTypes =
        { LocatorType.Id, LocatorType.XPath, LocatorType.ClassName, LocatorType.Name, LocatorType.AccessibilityId };

    public string Title => "Mobil (Android) tesztelés";
    public string Description => "Appium (UiAutomator2) alapú lépéslista, élő kijelző-tükrözéssel.";

    public ObservableCollection<TestStepRow> Steps { get; } = new();

    [ObservableProperty]
    private string testName = "";

    public IReadOnlyList<MobileStepAction> AvailableActions { get; } = Enum.GetValues<MobileStepAction>();
    public IReadOnlyList<LocatorType> AvailableLocatorTypes { get; } = SupportedLocatorTypes;
    public IReadOnlyList<string> SwipeDirections { get; } = new[] { "Fel", "Le", "Balra", "Jobbra" };

    [ObservableProperty]
    private MobileStepAction newAction = MobileStepAction.StartEmulator;

    [ObservableProperty]
    private LocatorType newLocatorType = LocatorType.Id;

    [ObservableProperty]
    private string newLocator = string.Empty;

    [ObservableProperty]
    private string newValue = string.Empty;

    [ObservableProperty]
    private int newTimeoutSeconds = 10;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private BitmapImage? screenImage;

    [ObservableProperty]
    private bool isMirroring;

    [ObservableProperty]
    private bool isPicking;

    public ObservableCollection<LocatorCandidate> InspectorCandidates { get; } = new();

    public bool HasInspectorResult => InspectorCandidates.Count > 0;

    private TestStepRow? _editingRow;

    public bool IsEditing => _editingRow is not null;
    public string AddButtonLabel => IsEditing ? "Mentés" : "Hozzáadás";

    public bool IsLocatorNeeded => !NoLocatorActions.Contains(NewAction);
    public bool IsValueNeeded => !NoValueActions.Contains(NewAction);
    public bool IsSwipeDirection => NewAction == MobileStepAction.Swipe;

    public MobileTestViewModel(MobileAutomationDriver driver, INotificationService notificationService, AT.Infrastructure.ISettingsService settingsService, AT.Infrastructure.ITestSuiteFileService fileService)
    {
        _driver = driver;
        _notificationService = notificationService;
        _fileService = fileService;
        Steps.CollectionChanged += (_, _) => RunStepsCommand.NotifyCanExecuteChanged();

        _mirrorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _mirrorTimer.Tick += async (_, _) => await RefreshScreenAsync();

        var defaults = settingsService.Current;
        _defaultTimeoutSeconds = defaults.DefaultTimeoutSeconds;
        _defaultAvdName = defaults.DefaultAvdName;
        _defaultApkPath = defaults.DefaultApkPath;
        NewTimeoutSeconds = _defaultTimeoutSeconds;
        _driver.SdkRootOverride = defaults.AndroidSdkRoot;
    }

    partial void OnNewActionChanged(MobileStepAction value)
    {
        OnPropertyChanged(nameof(IsLocatorNeeded));
        OnPropertyChanged(nameof(IsValueNeeded));
        OnPropertyChanged(nameof(IsSwipeDirection));

        if (string.IsNullOrWhiteSpace(NewValue))
        {
            if (value == MobileStepAction.StartEmulator && !string.IsNullOrWhiteSpace(_defaultAvdName))
                NewValue = _defaultAvdName;
            else if (value == MobileStepAction.LaunchApp && !string.IsNullOrWhiteSpace(_defaultApkPath))
                NewValue = _defaultApkPath;
        }
    }

    // ===================== ÉLŐ KIJELZŐ-TÜKRÖZÉS =====================
    // A régi DeviceDisplayManager egy sosem írt fájlt (device_screen.png) figyelt —
    // itt ténylegesen az Appium driver ad vissza egy valódi PNG-t minden ütemben.

    [RelayCommand]
    private void ToggleMirroring()
    {
        if (IsMirroring)
        {
            _mirrorTimer.Stop();
            IsMirroring = false;
            return;
        }

        if (!_driver.IsRunning)
        {
            _notificationService.Show("Nincs aktív session — indíts előbb egy alkalmazást (LaunchApp).", NotificationType.Warning);
            return;
        }

        _mirrorTimer.Start();
        IsMirroring = true;
    }

    private async Task RefreshScreenAsync()
    {
        if (!_driver.IsRunning)
        {
            _mirrorTimer.Stop();
            IsMirroring = false;
            return;
        }

        var bytes = await _driver.TryGetScreenshotAsync();
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

        if ((NewAction == MobileStepAction.StartEmulator || NewAction == MobileStepAction.LaunchApp)
            && string.IsNullOrWhiteSpace(NewValue))
        {
            _notificationService.Show("Add meg az AVD nevét vagy az .apk elérési útját az érték mezőben.", NotificationType.Warning);
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

        NewAction = Enum.Parse<MobileStepAction>(row.Step.Action);
        NewLocatorType = row.Step.LocatorType;
        NewLocator = row.Step.Locator ?? string.Empty;
        NewValue = row.Step.Value ?? string.Empty;
        NewTimeoutSeconds = row.Step.TimeoutSeconds;

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
            FileName = string.IsNullOrWhiteSpace(TestName) ? "mobil-lepesek.xml" : $"{TestName}.xml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await _fileService.SaveAsync(dialog.FileName, AutomationTarget.Android, Steps.Select(r => r.Step), TestName);
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
            CancelEdit();
            _notificationService.Show($"{file.Steps.Count} lépés betöltve.", NotificationType.Success);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Betöltés sikertelen: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingRow = null;

        NewAction = MobileStepAction.StartEmulator;
        NewLocator = string.Empty;
        NewValue = string.Empty;
        NewTimeoutSeconds = 10;

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
    private void TogglePicking()
    {
        if (!_driver.IsRunning)
        {
            _notificationService.Show("Nincs aktív session — indíts előbb egy LaunchApp lépést, majd kapcsold be a tükrözést.", NotificationType.Warning);
            return;
        }

        IsPicking = !IsPicking;
        InspectorCandidates.Clear();
        OnPropertyChanged(nameof(HasInspectorResult));

        if (IsPicking && !IsMirroring)
            ToggleMirroring();
    }

    /// <summary>Az élő kijelző képére kattintva hívja a code-behind (relatív, 0..1 koordinátával).</summary>
    public async Task CaptureElementAtAsync(double relativeX, double relativeY)
    {
        if (!IsPicking || relativeX is < 0 or > 1 || relativeY is < 0 or > 1)
            return;

        var info = await _driver.GetElementAtRelativePointAsync(relativeX, relativeY);

        InspectorCandidates.Clear();

        if (info is null)
        {
            _notificationService.Show("Nem található elem ezen a ponton.", NotificationType.Warning);
            OnPropertyChanged(nameof(HasInspectorResult));
            return;
        }

        AddInspectorCandidate(LocatorType.Id, "resource-id", info.ResourceId);
        AddInspectorCandidate(LocatorType.AccessibilityId, "content-desc", info.ContentDesc);
        AddInspectorCandidate(LocatorType.ClassName, "class", info.ClassName);

        OnPropertyChanged(nameof(HasInspectorResult));

        if (!HasInspectorResult)
            _notificationService.Show("Az elemnek nincs használható azonosítója.", NotificationType.Warning);
    }

    private void AddInspectorCandidate(LocatorType type, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            InspectorCandidates.Add(new LocatorCandidate { Type = type, Label = label, Value = value });
    }

    [RelayCommand]
    private void UseInspectorCandidate(LocatorCandidate? candidate)
    {
        if (candidate is null)
            return;

        NewLocatorType = candidate.Type;
        NewLocator = candidate.Value;
        InspectorCandidates.Clear();
        OnPropertyChanged(nameof(HasInspectorResult));
        IsPicking = false;
        _notificationService.Show("Lokátor beillesztve az elem-keresőből.", NotificationType.Success);
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

    private static string BuildStepName(MobileStepAction action, string locator, string value) => action switch
    {
        MobileStepAction.StartEmulator => $"Emulátor indítása → {value}",
        MobileStepAction.LaunchApp => $"Alkalmazás telepítése/indítása → {value}",
        MobileStepAction.Click => $"Kattintás → {locator}",
        MobileStepAction.LongPress => $"Hosszan nyomás → {locator}",
        MobileStepAction.SendKeys => $"Beírás ({locator}) → {value}",
        MobileStepAction.Clear => $"Mező ürítése → {locator}",
        MobileStepAction.Swipe => $"Húzás → {value}",
        MobileStepAction.ScrollToElement => $"Görgetés az elemig → {locator}",
        MobileStepAction.ReadAttribute => $"Attribútum kiolvasása ({locator}) → {value}",
        MobileStepAction.Wait => "Várakozás",
        MobileStepAction.WaitVisible => $"Várakozás láthatóra → {locator}",
        MobileStepAction.WaitPresent => $"Várakozás megjelenésre → {locator}",
        MobileStepAction.WaitAbsent => $"Várakozás eltűnésre → {locator}",
        MobileStepAction.WaitHasText => $"Várakozás szövegre ({locator}) → {value}",
        MobileStepAction.WaitHasAttribute => $"Várakozás attribútumra ({locator}) → {value}",
        MobileStepAction.Close => "Alkalmazás bezárása",
        MobileStepAction.StopEmulator => "Emulátor leállítása",
        _ => action.ToString()
    };
}