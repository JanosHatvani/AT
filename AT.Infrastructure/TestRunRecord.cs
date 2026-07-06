using AT.Core.Models;

namespace AT.Infrastructure;

/// <summary>
/// Egy teljes tesztfuttatás összegzése — ezt menti el a ITestRunHistoryService
/// JSON-ként a history-mappába, és ebből generál HTML riportot az ITestReportService.
/// A "Riport exportálása" gomb és az Előzmények nézet is ezt az objektumot használja.
/// </summary>
public sealed class TestRunRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>A teszt neve (a TestName mezőből) — üres is lehet, ha a felhasználó nem adott meg nevet.</summary>
    public string TestName { get; init; } = string.Empty;

    public AutomationTarget Target { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime FinishedAt { get; init; }

    public TimeSpan TotalDuration => FinishedAt - StartedAt;

    public int TotalSteps { get; init; }

    public int PassedCount { get; init; }

    public int FailedCount { get; init; }

    public int SkippedCount { get; init; }

    /// <summary>Igaz, ha legalább egy lépés hibára futott.</summary>
    public bool HasFailures => FailedCount > 0;

    public List<TestStepResult> StepResults { get; init; } = new();

    /// <summary>
    /// A futtatáshoz tartozó képernyőkép-mappa (ha készült kép) — ugyanaz, amit
    /// a ViewModel a futtatás elején létrehozott. Null, ha nem készült egyetlen
    /// képernyőkép sem (pl. ScreenshotCaptureMode.Never volt beállítva).
    /// </summary>
    public string? ScreenshotFolderPath { get; init; }
}

/// <summary>Egy lépés eredménye egy TestRunRecord-on belül.</summary>
public sealed class TestStepResult
{
    public string StepName { get; init; } = string.Empty;

    public TestStatus Status { get; init; }

    public TimeSpan? Duration { get; init; }

    public string? Message { get; init; }

    /// <summary>Ha ehhez a lépéshez készült képernyőkép, ennek teljes elérési útja.</summary>
    public string? ScreenshotPath { get; init; }
}
