using System.Windows;
using System.Windows.Input;
using AT.App.Services;
using AT.Automation.Web;
using AT.Core.Models;
using AT.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AT.App.Views;


// Az ütemezés paramétereinek (cadence, időpont, napok/hónap-nap) bekérése.
//
// LÉTREHOZÁS módban (Show) a hívó (Web/Desktop/Mobil ViewModel ScheduleTaskCommand-ja)
// adja át a teszt nevét, a célmodult és a már összeállított lépéssort.
//
// SZERKESZTÉS módban (ShowForEdit) egy meglévő ScheduledTask időzítését lehet módosítani —
// a teszt neve, célmodul és lépéssor NEM szerkeszthető innen (azokhoz a Web/Desktop/Mobil
// nézeten kell új ütemezést létrehozni), csak a cadence/időpont/napok.
//
// Mindkét módban a statikus Show/ShowForEdit metódus ModalDialog-ként nyitja meg az
// ablakot, és a bezáráskor null-t ad vissza, ha a felhasználó Mégse-t nyomott, egyébként
// a kész ScheduledTask-ot.

public partial class ScheduleTaskDialog : Window
{
    // Window saját DataContext-je — csak ehhez az ablakhoz tartozó, egyszerű bekérő állapot.
    public sealed partial class DialogState : ObservableObject
    {
        [ObservableProperty] private ScheduleCadence selectedCadence = ScheduleCadence.Daily;
        [ObservableProperty] private string hourText = "9";
        [ObservableProperty] private string minuteText = "0";
        [ObservableProperty] private string dayOfMonthText = "1";

        [ObservableProperty] private bool isMonday;
        [ObservableProperty] private bool isTuesday;
        [ObservableProperty] private bool isWednesday;
        [ObservableProperty] private bool isThursday;
        [ObservableProperty] private bool isFriday;
        [ObservableProperty] private bool isSaturday;
        [ObservableProperty] private bool isSunday;

        [ObservableProperty] private string? validationMessage;
        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
        partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationMessage));

        public IReadOnlyList<ScheduleCadence> CadenceOptions { get; } = Enum.GetValues<ScheduleCadence>();

        public bool IsHourFieldVisible => SelectedCadence != ScheduleCadence.Hourly;
        public bool IsWeeklyFieldsVisible => SelectedCadence == ScheduleCadence.Weekly;
        public bool IsMonthlyFieldsVisible => SelectedCadence == ScheduleCadence.Monthly;

        partial void OnSelectedCadenceChanged(ScheduleCadence value)
        {
            OnPropertyChanged(nameof(IsHourFieldVisible));
            OnPropertyChanged(nameof(IsWeeklyFieldsVisible));
            OnPropertyChanged(nameof(IsMonthlyFieldsVisible));
        }
    }

    private readonly string _testName;
    private readonly string _categoryId;
    private readonly AutomationTarget _target;
    private readonly IReadOnlyList<TestStep> _steps;
    private readonly BrowserType? _browser;

    // Ha ez nem null, a dialógus szerkesztő módban van — a Result összeállításakor
    // ennek az Id/IsEnabled/LastRunAt mezőit őrzi meg (nem hoz létre új Id-t, nem kapcsolja
    // vissza be a feladatot, ha az ki volt kapcsolva)
    private readonly ScheduledTask? _editingTask;

    public DialogState State { get; } = new();

    public ScheduledTask? Result { get; private set; }

    private ScheduleTaskDialog(string testName, string categoryId, AutomationTarget target, IReadOnlyList<TestStep> steps, BrowserType? browser, ScheduledTask? editingTask)
    {
        _testName = testName;
        _categoryId = categoryId;
        _target = target;
        _steps = steps;
        _browser = browser;
        _editingTask = editingTask;

        InitializeComponent();
        DataContext = State;

        if (editingTask is not null)
            LoadFromExistingTask(editingTask);
    }

    // Szerkesztő módban a mezők a meglévő feladat aktuális beállításaival töltődnek elő
    private void LoadFromExistingTask(ScheduledTask task)
    {
        Title = "Ütemezés szerkesztése";

        State.SelectedCadence = task.Cadence;
        State.HourText = task.Hour.ToString();
        State.MinuteText = task.Minute.ToString();
        State.DayOfMonthText = task.DayOfMonth.ToString();

        State.IsMonday = task.DaysOfWeek.Contains(DayOfWeek.Monday);
        State.IsTuesday = task.DaysOfWeek.Contains(DayOfWeek.Tuesday);
        State.IsWednesday = task.DaysOfWeek.Contains(DayOfWeek.Wednesday);
        State.IsThursday = task.DaysOfWeek.Contains(DayOfWeek.Thursday);
        State.IsFriday = task.DaysOfWeek.Contains(DayOfWeek.Friday);
        State.IsSaturday = task.DaysOfWeek.Contains(DayOfWeek.Saturday);
        State.IsSunday = task.DaysOfWeek.Contains(DayOfWeek.Sunday);
    }


    // Megnyitja a dialógust modálisan, ÚJ ütemezés létrehozásához. Visszaadja a kész
    // ScheduledTask-ot, vagy null-t, ha a felhasználó Mégse-t választott / bezárta az ablakot.

    public static ScheduledTask? Show(Window owner, string testName, string categoryId, AutomationTarget target, IReadOnlyList<TestStep> steps, BrowserType? browser = null)
    {
        var dialog = new ScheduleTaskDialog(testName, categoryId, target, steps, browser, editingTask: null) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }


    // Megnyitja a dialógust modálisan, egy MEGLÉVŐ ütemezés időzítésének (cadence, időpont,
    // napok) szerkesztéséhez — a teszt neve, célmodul és lépéssor nem szerkeszthető innen,
    // azokhoz a Web/Desktop/Mobil nézeten kell új ütemezést létrehozni. A visszaadott
    // ScheduledTask ugyanazt az Id-t, IsEnabled és LastRunAt értéket őrzi meg, mint az eredeti.

    public static ScheduledTask? ShowForEdit(Window owner, ScheduledTask existingTask)
    {
        var dialog = new ScheduleTaskDialog(existingTask.Name, existingTask.CategoryId, existingTask.Target, existingTask.Steps, null, existingTask) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildTask(out var task, out var error))
        {
            State.ValidationMessage = error;
            return;
        }

        Result = task;
        Close();
    }

    private bool TryBuildTask(out ScheduledTask? task, out string? error)
    {
        task = null;

        if (string.IsNullOrWhiteSpace(_testName))
        {
            error = "A teszt neve nem lehet üres.";
            return false;
        }

        if (_steps.Count == 0)
        {
            error = "Nincs felvett lépés — előbb vegyél fel legalább egy lépést a lépéssorba.";
            return false;
        }

        if (!int.TryParse(State.MinuteText, out var minute) || minute is < 0 or > 59)
        {
            error = "A perc mezőnek 0 és 59 közötti egész számnak kell lennie.";
            return false;
        }

        var hour = 0;
        if (State.SelectedCadence != ScheduleCadence.Hourly)
        {
            if (!int.TryParse(State.HourText, out hour) || hour is < 0 or > 23)
            {
                error = "Az óra mezőnek 0 és 23 közötti egész számnak kell lennie.";
                return false;
            }
        }

        var daysOfWeek = new List<DayOfWeek>();
        if (State.SelectedCadence == ScheduleCadence.Weekly)
        {
            if (State.IsMonday) daysOfWeek.Add(DayOfWeek.Monday);
            if (State.IsTuesday) daysOfWeek.Add(DayOfWeek.Tuesday);
            if (State.IsWednesday) daysOfWeek.Add(DayOfWeek.Wednesday);
            if (State.IsThursday) daysOfWeek.Add(DayOfWeek.Thursday);
            if (State.IsFriday) daysOfWeek.Add(DayOfWeek.Friday);
            if (State.IsSaturday) daysOfWeek.Add(DayOfWeek.Saturday);
            if (State.IsSunday) daysOfWeek.Add(DayOfWeek.Sunday);

            if (daysOfWeek.Count == 0)
            {
                error = "Heti ismétlődéshez legalább egy napot ki kell választani.";
                return false;
            }
        }

        var dayOfMonth = 1;
        if (State.SelectedCadence == ScheduleCadence.Monthly)
        {
            if (!int.TryParse(State.DayOfMonthText, out dayOfMonth) || dayOfMonth is < 1 or > 31)
            {
                error = "A hónap napjának 1 és 31 közötti egész számnak kell lennie.";
                return false;
            }
        }

        // Szerkesztő módban az Id, Browser, IsEnabled és LastRunAt az EREDETI feladatból
        // öröklődik (a Browser mezőt ez a dialógus szerkesztéskor nem kéri be újra, mert
        // a célmodul/böngésző a lépéssorral együtt "lezárt" adat — csak az időzítés
        // szerkeszthető itt, lásd az osztály doksi-kommentjét és a ShowForEdit metódust).

        var newTask = new ScheduledTask
        {
            Id = _editingTask?.Id ?? Guid.NewGuid().ToString("N"),
            Name = _testName,
            CategoryId = _editingTask?.CategoryId ?? _categoryId,
            Target = _target,
            Steps = _steps.ToList(),
            Browser = _editingTask?.Browser ?? _browser?.ToString(),
            Cadence = State.SelectedCadence,
            Hour = hour,
            Minute = minute,
            DaysOfWeek = daysOfWeek,
            DayOfMonth = dayOfMonth,
            IsEnabled = _editingTask?.IsEnabled ?? true,
            LastRunAt = _editingTask?.LastRunAt
        };

        newTask.NextRunAt = SchedulerService.ComputeNextRunAt(newTask, DateTime.Now);

        task = newTask;
        error = null;
        return true;
    }
}
