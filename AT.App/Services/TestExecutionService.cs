using System.Diagnostics;
using System.IO;
using AT.App.Models;
using AT.Automation.Desktop;
using AT.Automation.Mobile;
using AT.Automation.Web;
using AT.Core.Contracts;
using AT.Core.Models;
using AT.Infrastructure;

namespace AT.App.Services;

public interface ITestExecutionService
{
    /// <summary>
    /// Lefuttat egy lépéssort a megadott célmodulon — pontosan úgy, mint a Web/Desktop/Mobil
    /// nézetek "Futtatás" gombja: driver indítása, lépésenkénti végrehajtás, StepFlowResolver-alapú
    /// ugrás-feloldás, opcionális screenshot, majd history-mentés. A hívó (pl. SchedulerService)
    /// kapja vissza az eredmény-összegzést, hogy toast-ot vagy egyéb jelzést adhasson.
    /// </summary>
    /// <param name="browserName">Web célmodul esetén a böngésző neve (pl. "Chrome") — a
    /// BrowserType enum string-alakja. String, nem BrowserType, hogy a hívó (SchedulerService,
    /// ami az AT.Infrastructure-ből kapja a ScheduledTask.Browser stringet) ne függjön az
    /// AT.Automation.Web projekttől.</param>
    /// <param name="categoryId">A teszt-kategória Id-ja (TestCategory.Id) — a TestRunRecord-ba
    /// kerül, hogy az Előzmények nézet platform+kategória szerint tudjon szűrni.</param>
    Task<TestRunRecord> RunAsync(string testName, AutomationTarget target, IReadOnlyList<TestStep> steps, string categoryId, string? browserName = null);
}

/// <summary>
/// A Web/Desktop/Mobil ViewModel-ekben lévő RunStepsCoreAsync logika modul-független
/// kiszervezése. Ezt használja a ScheduledTask-ok automatikus futtatása, hogy a lépéssor
/// pontosan úgy fusson le, mintha a felhasználó kézzel indította volna el a megfelelő
/// modulban — ugyanaz a driver, ugyanaz az ugrás-feloldás, ugyanaz a history-bejegyzés.
///
/// FONTOS: ez a szolgáltatás nem ismeri a UI-oldali TestStepRow-t (Status/Duration/Message
/// megjelenítést) — csak a nyers TestStep-eket hajtja végre és egy TestRunRecord-ot ad
/// vissza. Ha egy ütemezett teszt éppen akkor futna, amikor a felhasználó ugyanabban a
/// modulban kézzel is dolgozik, a hívó (SchedulerService) felelőssége a sorba állítás.
/// </summary>
public sealed class TestExecutionService : ITestExecutionService
{
    private readonly WebAutomationDriver _webDriver;
    private readonly DesktopAutomationDriver _desktopDriver;
    private readonly MobileAutomationDriver _mobileDriver;
    private readonly ISettingsService _settingsService;
    private readonly ITestRunHistoryService _historyService;
    private readonly INotificationService _notificationService;
    private readonly IEmailNotificationService _emailNotificationService;

    public TestExecutionService(
        WebAutomationDriver webDriver,
        DesktopAutomationDriver desktopDriver,
        MobileAutomationDriver mobileDriver,
        ISettingsService settingsService,
        ITestRunHistoryService historyService,
        INotificationService notificationService,
        IEmailNotificationService emailNotificationService)
    {
        _webDriver = webDriver;
        _desktopDriver = desktopDriver;
        _mobileDriver = mobileDriver;
        _settingsService = settingsService;
        _historyService = historyService;
        _notificationService = notificationService;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<TestRunRecord> RunAsync(string testName, AutomationTarget target, IReadOnlyList<TestStep> steps, string categoryId, string? browserName = null)
    {
        var startedAt = DateTime.Now;
        var screenshotFolder = ResolveRunScreenshotFolder(testName, startedAt);

        var results = steps.Select(s => new StepRunState(s)).ToList();

        try
        {
            await StartDriverAsync(target, browserName);

            var currentIndex = 0;
            var executionCount = 0;
            var hitExecutionLimit = false;

            while (currentIndex >= 0 && currentIndex < results.Count)
            {
                executionCount++;
                if (executionCount > StepFlowResolver.MaxStepExecutions)
                {
                    hitExecutionLimit = true;
                    break;
                }

                var state = results[currentIndex];

                if (state.Step.Skip)
                {
                    state.Status = TestStatus.Skipped;
                    currentIndex++;
                    continue;
                }

                var stopwatch = Stopwatch.StartNew();
                var outcome = await StepRetryExecutor.ExecuteWithRetryAsync(
                    () => ExecuteStepAsync(target, state.Step),
                    state.Step.RetryCount);
                stopwatch.Stop();

                state.Duration = stopwatch.Elapsed;
                state.AttemptCount = outcome.AttemptCount;
                var wasSuccess = outcome.Succeeded;

                if (wasSuccess)
                {
                    state.Status = TestStatus.Passed;
                    await CaptureScreenshotIfNeededAsync(target, state, screenshotFolder, isFailure: false);
                }
                else
                {
                    state.Status = TestStatus.Failed;
                    state.Message = outcome.ErrorMessage;
                    await CaptureScreenshotIfNeededAsync(target, state, screenshotFolder, isFailure: true);
                }

                var stepList = results.Select(r => r.Step).ToList();
                var nextIndex = StepFlowResolver.ResolveNextIndex(
                    stepList, currentIndex, wasSuccess, state.Step.ContinueOnError, out var shouldStop);

                if (shouldStop)
                    break;

                currentIndex = nextIndex ?? results.Count;
            }

            if (hitExecutionLimit)
            {
                _notificationService.Show(
                    $"Ütemezett futtatás leállt: több mint {StepFlowResolver.MaxStepExecutions} lépés futott le ({testName}) — valószínűleg végtelen ciklusba került az ugrások miatt.",
                    NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Ütemezett futtatás indítása sikertelen ({testName}): {ex.Message}", NotificationType.Error);
        }

        var finishedAt = DateTime.Now;

        var record = new TestRunRecord
        {
            TestName = testName,
            CategoryId = categoryId,
            Target = target,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            TotalSteps = results.Count,
            PassedCount = results.Count(s => s.Status == TestStatus.Passed),
            FailedCount = results.Count(s => s.Status == TestStatus.Failed),
            SkippedCount = results.Count(s => s.Status == TestStatus.Skipped),
            ScreenshotFolderPath = screenshotFolder,
            StepResults = results.Select(s => new TestStepResult
            {
                StepName = s.Step.Name,
                Status = s.Status,
                Duration = s.Duration,
                Message = s.Message,
                ScreenshotPath = s.ScreenshotPath,
                AttemptCount = s.AttemptCount
            }).ToList()
        };

        try
        {
            await _historyService.SaveRunAsync(record);
        }
        catch (Exception ex)
        {
            _notificationService.Show($"Ütemezett futtatás előzményének mentése sikertelen ({testName}): {ex.Message}", NotificationType.Warning);
        }

        var hasFailed = record.HasFailures;
        _notificationService.Show(
            hasFailed
                ? $"Ütemezett teszt lefutott, de hibával: {testName}."
                : $"Ütemezett teszt sikeresen lefutott: {testName}.",
            hasFailed ? NotificationType.Error : NotificationType.Success);

        // Riport-email csak hiba esetén megy ki, és csak akkor, ha a Beállításokban be van
        // kapcsolva (lásd EmailNotificationService.SendFailureReportAsync — ott dől el, hogy
        // az EmailNotificationsEnabled be van-e kapcsolva, és vannak-e érvényes SMTP-adatok).
        // Ez a szolgáltatás jelenleg kizárólag a SchedulerService-ből hívódik (a Web/Desktop/
        // Mobil ViewModel-ek kézi futtatása a saját RunStepsCoreAsync-jüket használja, nem ezt),
        // tehát minden ide érkező futtatás definíció szerint ütemezett — nincs szükség külön
        // isScheduled paraméterre ahhoz, hogy csak ütemezett hibánál küldjön emailt.
        if (hasFailed)
        {
            try
            {
                await _emailNotificationService.SendFailureReportAsync(record);
            }
            catch (Exception ex)
            {
                // Az EmailNotificationService már maga sem dob kivételt normál esetben (lásd
                // ott a try-catch-et), de ez egy extra biztonsági háló, hogy egy esetleges,
                // előre nem látott hiba az email-küldésben semmiképp ne akassza meg vagy
                // buktassa el az egész ütemezett futtatást.
                _notificationService.Show($"Riport-email küldése váratlan hibával leállt ({testName}): {ex.Message}", NotificationType.Warning);
            }
        }

        return record;
    }

    private async Task StartDriverAsync(AutomationTarget target, string? browserName)
    {
        switch (target)
        {
            case AutomationTarget.Web:
                if (!string.IsNullOrWhiteSpace(browserName) && Enum.TryParse<BrowserType>(browserName, ignoreCase: true, out var browser))
                    _webDriver.Browser = browser;
                await _webDriver.StartAsync();
                break;

            case AutomationTarget.Desktop:
                await _desktopDriver.StartAsync();
                break;

            case AutomationTarget.Android:
                await _mobileDriver.StartAsync();
                break;
        }
    }

    private Task ExecuteStepAsync(AutomationTarget target, TestStep step) => target switch
    {
        AutomationTarget.Web => _webDriver.ExecuteStepAsync(step),
        AutomationTarget.Desktop => _desktopDriver.ExecuteStepAsync(step),
        AutomationTarget.Android => _mobileDriver.ExecuteStepAsync(step),
        _ => Task.CompletedTask
    };

    private async Task<byte[]?> TryGetScreenshotAsync(AutomationTarget target)
    {
        try
        {
            return target switch
            {
                AutomationTarget.Web => await _webDriver.GetScreenshotAsync(),
                AutomationTarget.Desktop => await _desktopDriver.GetScreenshotAsync(),
                AutomationTarget.Android => await _mobileDriver.GetScreenshotAsync(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveRunScreenshotFolder(string testName, DateTime startedAt)
    {
        if (_settingsService.Current.ScreenshotCaptureMode == ScreenshotCaptureMode.Never)
            return null;

        var baseFolder = string.IsNullOrWhiteSpace(_settingsService.Current.ScreenshotFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _settingsService.Current.ScreenshotFolderPath!;

        return ScreenshotFolderResolver.CreateRunFolder(baseFolder, testName, startedAt);
    }

    private async Task CaptureScreenshotIfNeededAsync(AutomationTarget target, StepRunState state, string? screenshotFolder, bool isFailure)
    {
        var mode = _settingsService.Current.ScreenshotCaptureMode;
        var shouldCapture = mode == ScreenshotCaptureMode.Always
            || (isFailure && mode == ScreenshotCaptureMode.OnErrorOnly);

        if (!shouldCapture || screenshotFolder is null)
            return;

        var bytes = await TryGetScreenshotAsync(target);
        if (bytes is null)
            return;

        try
        {
            var fileName = $"{SanitizeFileName(state.Step.Name)}_{DateTime.Now:HHmmss_fff}.png";
            var fullPath = Path.Combine(screenshotFolder, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes);
            state.ScreenshotPath = fullPath;
        }
        catch
        {
            // Ütemezett, felügyelet nélküli futtatásnál a képmentés hibája nem szakíthatja
            // meg a tesztet — legfeljebb az adott lépéshez nem lesz képernyőkép.
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    /// <summary>Egy lépés futásközbeni állapota — a UI-independens megfelelője a TestStepRow-nak.</summary>
    private sealed class StepRunState
    {
        public StepRunState(TestStep step) => Step = step;

        public TestStep Step { get; }
        public TestStatus Status { get; set; } = TestStatus.NotRun;
        public TimeSpan? Duration { get; set; }
        public string? Message { get; set; }
        public string? ScreenshotPath { get; set; }
        public int AttemptCount { get; set; } = 1;
    }
}
