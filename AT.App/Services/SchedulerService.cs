using System.Windows.Threading;
using AT.Infrastructure;

namespace AT.App.Services;

public interface ISchedulerService
{
    // Elindítja a percenkénti ellenőrzést. Az App.xaml.cs induláskor egyszer hívja meg.

    // Leállítja az időzítőt (pl. az alkalmazás bezárásakor)
    void Stop();
    void Start();

    // Jelzi, hogy egy adott modul jelenleg kézzel foglalt-e (pl. a felhasználó épp futtat
    // valamit a Web nézetben). A ViewModel-ek RunStepsCoreAsync eleje/vége jelentkezik be/ki
    // ezen keresztül — a scheduler ez alapján dönt a sorba állításról.

    void SetModuleBusy(AT.Core.Models.AutomationTarget target, bool isBusy);

    // Kényszeríti egy adott feladat NextRunAt mezőjének újraszámítását (pl. létrehozás/szerkesztés után).
    void RecalculateNextRun(ScheduledTask task);
}


// Belső, DispatcherTimer-alapú ütemező: az AT.App-nak futnia/megnyitva kell lennie ahhoz,
// hogy egy ütemezett feladat lefusson (nincs Windows Task Scheduler-integráció, nincs
// külön szolgáltatás-folyamat). Percenként ellenőrzi, van-e esedékes (IsEnabled és
// NextRunAt &lt;= Now) feladat; ha a célmodul épp foglalt, egy belső várólistába teszi,
// és a modul felszabadulásakor futtatja le.

public sealed class SchedulerService : ISchedulerService
{
    private readonly DispatcherTimer _timer;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly ITestExecutionService _executionService;

    private readonly HashSet<AT.Core.Models.AutomationTarget> _busyModules = new();
    private readonly Queue<ScheduledTask> _pendingQueue = new();
    private bool _isProcessingQueue;


    // Igaz, ha a ProcessQueueAsync egy futó példánya alatt érkezett egy újabb "próbáld
    // meg feldolgozni a sort" igény (pl. egy modul épp most szabadult fel) — enélkül ez
    // az igény elveszne, mert a párhuzamosan induló ProcessQueueAsync hívás az
    // _isProcessingQueue miatt azonnal visszatérne, anélkül hogy bármit csinálna. A futó
    // példány a finally ágában ellenőrzi ezt a jelzőt, és ha igaz, újraindítja magát.

    private bool _reprocessRequested;

    public SchedulerService(IScheduledTaskService scheduledTaskService, ITestExecutionService executionService)
    {
        _scheduledTaskService = scheduledTaskService;
        _executionService = executionService;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += async (_, _) => await CheckDueTasksAsync();
    }

    public void Start()
    {
        // Induláskor azonnal egy ellenőrzés is lefut (nem csak az első Tick-nél, ami
        // akár egy percet is várathatna) — így egy program-indítás pillanatában is
        // esedékes feladat rögtön elindulhat, kilépés/újranyitás után is helyesen.
        _ = CheckDueTasksAsync();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void SetModuleBusy(AT.Core.Models.AutomationTarget target, bool isBusy)
    {
        if (isBusy)
            _busyModules.Add(target);
        else
            _busyModules.Remove(target);

        // Ha egy modul most szabadult fel, próbáljuk meg azonnal lefuttatni,
        // ami esetleg rá vár a várólistában — nem kell megvárni a következő percet.
        if (!isBusy)
            _ = ProcessQueueAsync();
    }

    public void RecalculateNextRun(ScheduledTask task)
    {
        task.NextRunAt = ComputeNextRunAt(task, DateTime.Now);
    }

    private async Task CheckDueTasksAsync()
    {
        var now = DateTime.Now;
        var due = _scheduledTaskService.Tasks
            .Where(t => t.IsEnabled && t.NextRunAt.HasValue && t.NextRunAt.Value <= now)
            .ToList();

        foreach (var task in due)
        {
            // A NextRunAt-ot MÉG a futtatás előtt előre visszük a következő esedékességre,
            // hogy egy hosszan futó teszt alatt a timer következő Tick-je ne állítsa be
            // ugyanazt a feladatot még egyszer esedékesnek.
            task.LastRunAt = now;
            task.NextRunAt = ComputeNextRunAt(task, now);
            await _scheduledTaskService.UpdateAsync(task);

            if (!_pendingQueue.Contains(task))
                _pendingQueue.Enqueue(task);
        }

        if (due.Count > 0)
            await ProcessQueueAsync();
    }


    // A várólistában lévő feladatok közül azokat, amiknek a célmodulja épp szabad,
    // EGYSZERRE (párhuzamosan) indítja el — pl. egy esedékes Web- és egy esedékes
    // Desktop-teszt egy időben fut, mert a driverjeik függetlenek egymástól. Egy adott
    // célmodulon belül viszont mindig csak egy futtatás mehet egyszerre (ugyanazt a
    // driver-példányt nem lehet két helyről egyszerre használni) — ha egy modul már
    // foglalt (akár egy másik ütemezett feladat, akár egy kézi futtatás miatt — lásd
    // SetModuleBusy), az adott célmodulú feladat a sorban marad, amíg fel nem szabadul.

    private async Task ProcessQueueAsync()
    {
        if (_isProcessingQueue)
        {
            // Már fut egy példány — jelöljük, hogy amikor végez, még egyszer nézze át a
            // sort, mert időközben (pl. épp most) újabb esedékesség/felszabadulás történt,
            // amit ez a hívás jelzett volna, ha nem ütközik bele egy már futó példányba.
            _reprocessRequested = true;
            return;
        }

        _isProcessingQueue = true;
        try
        {
            var startedTasks = new List<Task>();

            // Egy körben végigmegyünk a teljes várólistán, és minden feladatot elindítunk
            // (de nem várunk meg), aminek a célmodulja ÉPP MOST szabad. Ezzel a különböző
            // célmodulú feladatok valóban párhuzamosan futnak. Az azonos célmodulú
            // feladatok közül csak az első indul el ebben a körben — a második a
            // _busyModules-ban már foglaltnak látja a modult (hiszen az elsőt épp
            // elindítottuk), és a sorban marad a következő körig (amit a most induló
            // feladat befejezése vált ki, lásd a finally ágban a SetModuleBusy(false)-t).
            var remaining = new Queue<ScheduledTask>(_pendingQueue);
            _pendingQueue.Clear();

            while (remaining.Count > 0)
            {
                var next = remaining.Dequeue();

                if (_busyModules.Contains(next.Target))
                {
                    // A célmodul foglalt (vagy mert egy másik, ugyanebben a körben most
                    // induló feladat lefoglalta, vagy mert kézi futtatás van folyamatban)
                    // — visszatesszük a várólistába, hogy a modul felszabadulásakor
                    // (SetModuleBusy(false) -> ProcessQueueAsync újrahívás) esedékes legyen.
                    _pendingQueue.Enqueue(next);
                    continue;
                }

                _busyModules.Add(next.Target);

                var runTask = RunAndReleaseAsync(next);
                startedTasks.Add(runTask);
            }

            // Nem várjuk meg itt a startedTasks-ot (ez blokkolná a metódust, amíg a
            // leghosszabb futtatás be nem fejeződik) — a metódus visszatér, amint minden,
            // ebben a körben induló feladat elindult. A futásuk a háttérben folytatódik,
            // és a befejezésükkor a RunAndReleaseAsync finally ága gondoskodik a
            // _busyModules felszabadításáról és a várólista újra-feldolgozásáról.
        }
        finally
        {
            _isProcessingQueue = false;

            if (_reprocessRequested)
            {
                _reprocessRequested = false;
                _ = ProcessQueueAsync();
            }
        }
    }

    // Lefuttat egy feladatot, majd garantáltan felszabadítja a célmodult és
    // újra megpróbálja feldolgozni a várólistát — akkor is, ha a futtatás közben
    // kivétel történt (bár az ITestExecutionService.RunAsync normál esetben nem dob)
    private async Task RunAndReleaseAsync(ScheduledTask task)
    {
        try
        {
            await _executionService.RunAsync(task.Name, task.Target, task.Steps, task.CategoryId, task.Browser);
        }
        finally
        {
            _busyModules.Remove(task.Target);
            _ = ProcessQueueAsync();
        }
    }


    // Kiszámítja a cadence, óra:perc és nap-specifikáció alapján a legközelebbi jövőbeli
    // időpontot, amikor a feladatnak le kell futnia — mindig szigorúan 'from' után.

    public static DateTime ComputeNextRunAt(ScheduledTask task, DateTime from)
    {
        switch (task.Cadence)
        {
            case ScheduleCadence.Hourly:
            {
                var candidate = new DateTime(from.Year, from.Month, from.Day, from.Hour, task.Minute, 0);
                if (candidate <= from)
                    candidate = candidate.AddHours(1);
                return candidate;
            }

            case ScheduleCadence.Daily:
            {
                var candidate = new DateTime(from.Year, from.Month, from.Day, task.Hour, task.Minute, 0);
                if (candidate <= from)
                    candidate = candidate.AddDays(1);
                return candidate;
            }

            case ScheduleCadence.Weekly:
            {
                if (task.DaysOfWeek.Count == 0)
                {
                    // Nincs kiválasztott nap — biztonsági tartalék, hogy sose ragadjon be
                    // egy soha esedékessé nem váló feladat: minden nap fusson.
                    var fallback = new DateTime(from.Year, from.Month, from.Day, task.Hour, task.Minute, 0);
                    if (fallback <= from)
                        fallback = fallback.AddDays(1);
                    return fallback;
                }

                for (var offset = 0; offset <= 7; offset++)
                {
                    var day = from.Date.AddDays(offset);
                    if (!task.DaysOfWeek.Contains(day.DayOfWeek))
                        continue;

                    var candidate = new DateTime(day.Year, day.Month, day.Day, task.Hour, task.Minute, 0);
                    if (candidate > from)
                        return candidate;
                }

                // Elméletileg nem fordulhat elő (legfeljebb egy hét múlva mindig van találat),
                // de a fordítónak és a robusztusságnak kedvéért egy explicit tartalék:
                return from.AddDays(7);
            }

            case ScheduleCadence.Monthly:
            {
                var day = Math.Clamp(task.DayOfMonth, 1, DateTime.DaysInMonth(from.Year, from.Month));
                var candidate = new DateTime(from.Year, from.Month, day, task.Hour, task.Minute, 0);

                if (candidate <= from)
                {
                    var nextMonth = from.AddMonths(1);
                    var nextDay = Math.Clamp(task.DayOfMonth, 1, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    candidate = new DateTime(nextMonth.Year, nextMonth.Month, nextDay, task.Hour, task.Minute, 0);
                }

                return candidate;
            }

            default:
                return from.AddDays(1);
        }
    }
}
