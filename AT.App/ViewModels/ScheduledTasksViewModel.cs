using System.Collections.ObjectModel;
using AT.App.Models;
using AT.App.Services;
using AT.Core.Models;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AT.App.ViewModels;

/// <summary>Platform-szűrő opció az Ütemezett feladatok/Előzmények listájához — "Mind" esetén
/// minden platform és minden kategória látszik, egyébként csak az adott platformhoz tartozó
/// kategóriák jelennek meg választhatóként, és csak az annak megfelelő elemek a listában.</summary>
public enum PlatformFilter
{
    All,
    Web,
    Desktop,
    Mobile
}

/// <summary>
/// Az "Ütemezett feladatok" nézet ViewModel-je — kártyás listában jeleníti meg a
/// ScheduledTaskService-ben tárolt feladatokat, minden kártya lenyitható (a beágyazott
/// lépéssor megtekintésére), és soronként be/kikapcsolható vagy törölhető. A tényleges
/// létrehozás a Web/Desktop/Mobil nézetek "Ütemezés létrehozása" gombjával, a
/// ScheduleTaskDialog-on keresztül történik — ez a nézet elsősorban áttekintésre és
/// karbantartásra való. Platform + kategória szerint szűrhető (két ComboBox a lista felett).
/// </summary>
public sealed partial class ScheduledTasksViewModel : ObservableObject
{
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ISchedulerService _schedulerService;
    private readonly INotificationService _notificationService;
    private readonly ITestCategoryService _categoryService;

    public string Title => "Ütemezett feladatok";
    public string Description => "Automatikusan, a program futása közben, a megadott időpontokban lefutó tesztek — az eredmény ugyanúgy megjelenik az Előzmények oldalon és toast-üzenetként, mint egy kézi futtatásnál.";

    public ObservableCollection<ScheduledTaskRow> Rows { get; } = new();

    public bool HasRows => Rows.Count > 0;

    [ObservableProperty]
    private bool isLoading;

    // ===================== PLATFORM + KATEGÓRIA SZŰRŐ =====================

    public IReadOnlyList<PlatformFilter> PlatformFilterOptions { get; } = Enum.GetValues<PlatformFilter>();

    [ObservableProperty]
    private PlatformFilter selectedPlatformFilter = PlatformFilter.All;

    /// <summary>"Mind" opció + a kiválasztott platformra (vagy minden platformra, ha "Mind"
    /// van kiválasztva) engedélyezett kategóriák. A "Mind" kategória-opciót null Id
    /// reprezentálja a listában (lásd CategoryFilterOption).</summary>
    public ObservableCollection<CategoryFilterOption> AvailableCategoryFilters { get; } = new();

    [ObservableProperty]
    private CategoryFilterOption? selectedCategoryFilter;

    partial void OnSelectedPlatformFilterChanged(PlatformFilter value)
    {
        RebuildCategoryFilterOptions();
        LoadRows();
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value) => LoadRows();

    private void RebuildCategoryFilterOptions()
    {
        var previousSelectionId = SelectedCategoryFilter?.CategoryId;

        AvailableCategoryFilters.Clear();
        AvailableCategoryFilters.Add(CategoryFilterOption.AllOption);

        var relevantCategories = SelectedPlatformFilter switch
        {
            PlatformFilter.Web => _categoryService.GetCategoriesForTarget(AutomationTarget.Web),
            PlatformFilter.Desktop => _categoryService.GetCategoriesForTarget(AutomationTarget.Desktop),
            PlatformFilter.Mobile => _categoryService.GetCategoriesForTarget(AutomationTarget.Android),
            _ => _categoryService.Categories
        };

        foreach (var category in relevantCategories.OrderBy(c => c.Name))
            AvailableCategoryFilters.Add(new CategoryFilterOption(category.Id, category.Name));

        // Ha az előzőleg kiválasztott kategória még mindig szerepel az (esetleg megváltozott)
        // listában, megtartjuk — egyébként visszaesünk "Mind"-ra, hogy sose maradjon egy
        // érvénytelen, a listában nem is szereplő kiválasztás.
        SelectedCategoryFilter = AvailableCategoryFilters.FirstOrDefault(c => c.CategoryId == previousSelectionId)
            ?? CategoryFilterOption.AllOption;
    }

    public ScheduledTasksViewModel(
        IScheduledTaskService scheduledTaskService,
        ISchedulerService schedulerService,
        INotificationService notificationService,
        ITestCategoryService categoryService)
    {
        _scheduledTaskService = scheduledTaskService;
        _schedulerService = schedulerService;
        _notificationService = notificationService;
        _categoryService = categoryService;

        RebuildCategoryFilterOptions();
        LoadRows();
    }

    /// <summary>
    /// A ScheduledTaskService.LoadAsync-ot az App.xaml.cs induláskor egyszer meghívja —
    /// ez a metódus csak a már betöltött, memóriában lévő Tasks listából építi fel a
    /// UI-sorokat, a jelenlegi platform+kategória szűrő szerint. A "Frissítés" gomb
    /// (lásd ScheduledTasksView.xaml) erre a parancsra kötődik — mivel a
    /// ScheduledTasksViewModel Singleton (csak egyszer épül fel a konstruktorban), ha a
    /// Web/Desktop/Mobil nézeten közben új ütemezés jön létre, azt csak egy explicit
    /// frissítéssel látja meg ez a nézet.
    /// </summary>
    [RelayCommand]
    private void LoadRows()
    {
        Rows.Clear();

        var filtered = _scheduledTaskService.Tasks.Where(MatchesFilter).OrderBy(t => t.NextRunAt);
        foreach (var task in filtered)
        {
            var categoryLabel = _categoryService.Categories.FirstOrDefault(c => c.Id == task.CategoryId)?.Name ?? "Kategória nélkül";
            Rows.Add(new ScheduledTaskRow(task, categoryLabel));
        }

        OnPropertyChanged(nameof(HasRows));
    }

    private bool MatchesFilter(ScheduledTask task)
    {
        var platformMatches = SelectedPlatformFilter switch
        {
            PlatformFilter.Web => task.Target == AutomationTarget.Web,
            PlatformFilter.Desktop => task.Target == AutomationTarget.Desktop,
            PlatformFilter.Mobile => task.Target == AutomationTarget.Android,
            _ => true
        };

        if (!platformMatches)
            return false;

        // "Mind" kategória-opció esetén (CategoryId == null) minden kategória átmegy a szűrőn.
        if (SelectedCategoryFilter is null || SelectedCategoryFilter.CategoryId is null)
            return true;

        return task.CategoryId == SelectedCategoryFilter.CategoryId;
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(ScheduledTaskRow? row)
    {
        if (row is null)
            return;

        row.Task.IsEnabled = !row.Task.IsEnabled;

        if (row.Task.IsEnabled)
            _schedulerService.RecalculateNextRun(row.Task);

        await _scheduledTaskService.UpdateAsync(row.Task);

        // A ScheduledTaskRow nem ObservableObject (egyszerű, egyszeri-feltöltésű wrapper),
        // ezért a legegyszerűbb, hibamentes frissítés a teljes lista újraépítése a
        // frissen elmentett állapotból — ez a lenyitott Expander-eket visszazárja, de a
        // Toggle/Delete amúgy sem gyakori, egymást követő művelet, úgyhogy ez elfogadható.
        LoadRows();

        _notificationService.Show(
            row.Task.IsEnabled ? "Ütemezés bekapcsolva." : "Ütemezés kikapcsolva.",
            NotificationType.Info);
    }

    /// <summary>
    /// Megnyitja a ScheduleTaskDialog-ot szerkesztő módban (ShowForEdit) — csak a cadence,
    /// időpont és napok módosíthatók innen; a teszt neve, célmodul és lépéssor nem (azokhoz
    /// a Web/Desktop/Mobil nézeten kell új ütemezést létrehozni). Mégse esetén (null Result)
    /// semmi nem változik.
    /// </summary>
    [RelayCommand]
    private async Task EditAsync(ScheduledTaskRow? row)
    {
        if (row is null)
            return;

        var updatedTask = AT.App.Views.ScheduleTaskDialog.ShowForEdit(
            System.Windows.Application.Current.MainWindow,
            row.Task);

        if (updatedTask is null)
            return;

        _schedulerService.RecalculateNextRun(updatedTask);
        await _scheduledTaskService.UpdateAsync(updatedTask);
        LoadRows();

        _notificationService.Show("Ütemezés frissítve.", NotificationType.Success);
    }

    [RelayCommand]
    private async Task DeleteAsync(ScheduledTaskRow? row)
    {
        if (row is null)
            return;

        var confirmed = AT.App.Views.ConfirmDialog.Show(
            System.Windows.Application.Current.MainWindow,
            "Ütemezés törlése",
            $"Biztosan törlöd a(z) \"{row.Task.Name}\" ütemezést? Ez nem vonható vissza.",
            confirmButtonText: "Törlés",
            isDestructive: true);

        if (!confirmed)
            return;

        await _scheduledTaskService.DeleteAsync(row.Task.Id);
        Rows.Remove(row);
        OnPropertyChanged(nameof(HasRows));

        _notificationService.Show("Ütemezés törölve.", NotificationType.Info);
    }
}

/// <summary>UI-oldali wrapper egy ScheduledTask köré — a kártyás megjelenítéshez előfeldolgozott,
/// olvasható szövegekkel (cadence, időpont, napok, lépésszám, kategória-név).</summary>
public sealed class ScheduledTaskRow
{
    public ScheduledTask Task { get; }

    /// <summary>Előre feloldott kategória-név (a TestCategory.Id alapján) — mert a
    /// ScheduledTaskRow-nak magának nincs hozzáférése a ITestCategoryService-hez.</summary>
    public string CategoryLabel { get; }

    public ScheduledTaskRow(ScheduledTask task, string categoryLabel)
    {
        Task = task;
        CategoryLabel = categoryLabel;
    }

    public string Name => Task.Name;

    public string TargetLabel => Task.Target switch
    {
        AutomationTarget.Web => "Web",
        AutomationTarget.Desktop => "Windows desktop",
        AutomationTarget.Android => "Mobil (Android)",
        _ => Task.Target.ToString()
    };

    public string CadenceLabel => Task.Cadence switch
    {
        ScheduleCadence.Hourly => "Óránként",
        ScheduleCadence.Daily => "Naponta",
        ScheduleCadence.Weekly => "Hetente",
        ScheduleCadence.Monthly => "Havonta",
        _ => Task.Cadence.ToString()
    };

    public string ScheduleDetailText => Task.Cadence switch
    {
        ScheduleCadence.Hourly => $"minden órában, {Task.Minute:00} perckor",
        ScheduleCadence.Daily => $"minden nap {Task.Hour:00}:{Task.Minute:00}",
        ScheduleCadence.Weekly => Task.DaysOfWeek.Count == 0
            ? $"{Task.Hour:00}:{Task.Minute:00}"
            : $"{string.Join(", ", Task.DaysOfWeek.Select(DayName))} — {Task.Hour:00}:{Task.Minute:00}",
        ScheduleCadence.Monthly => $"minden hónap {Task.DayOfMonth}. napján, {Task.Hour:00}:{Task.Minute:00}",
        _ => ""
    };

    public int StepCount => Task.Steps.Count;

    public bool IsEnabled => Task.IsEnabled;

    public string LastRunText => Task.LastRunAt.HasValue
        ? Task.LastRunAt.Value.ToString("yyyy.MM.dd. HH:mm")
        : "még nem futott";

    public string NextRunText => !Task.IsEnabled
        ? "kikapcsolva"
        : Task.NextRunAt.HasValue
            ? Task.NextRunAt.Value.ToString("yyyy.MM.dd. HH:mm")
            : "—";

    public IReadOnlyList<TestStep> Steps => Task.Steps;

    private static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "H",
        DayOfWeek.Tuesday => "K",
        DayOfWeek.Wednesday => "Sze",
        DayOfWeek.Thursday => "Cs",
        DayOfWeek.Friday => "P",
        DayOfWeek.Saturday => "Szo",
        DayOfWeek.Sunday => "V",
        _ => day.ToString()
    };
}

/// <summary>Egy elem a kategória-szűrő ComboBox-ban — a "Mind" opciót null CategoryId
/// reprezentálja (ilyenkor minden kategória átmegy a szűrőn, lásd MatchesFilter).</summary>
public sealed class CategoryFilterOption
{
    public static readonly CategoryFilterOption AllOption = new(null, "Mind");

    public string? CategoryId { get; }
    public string Name { get; }

    public CategoryFilterOption(string? categoryId, string name)
    {
        CategoryId = categoryId;
        Name = name;
    }

    public override string ToString() => Name;
}
