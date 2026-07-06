using AT.Core.Contracts;

namespace AT.Core.Models;

public enum TestStatus
{
    NotRun,
    Running,
    Passed,
    Failed,
    Skipped
}

public enum AutomationTarget
{
    Web,
    Desktop,
    Android,
    Ios
}

/// <summary>Egy összeállított teszt-lépés a felhasználói lépéslistában.</summary>
public sealed class TestStep
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required AutomationTarget Target { get; set; }
    public required string Action { get; set; }
    public string? Locator { get; set; }
    public LocatorType LocatorType { get; set; } = LocatorType.Id;
    public string? Value { get; set; }

    /// <summary>Csak a Drag&amp;Drop lépéshez: a cél elem lokátora.</summary>
    public string? TargetLocator { get; set; }
    public LocatorType TargetLocatorType { get; set; } = LocatorType.Id;

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Ha true, a lépés hibája nem szakítja meg a futtatást — a lista folytatódik.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Ha true, a lépést a futtatás átugorja — meg sem kísérli végrehajtani.</summary>
    public bool Skip { get; set; }
}

/// <summary>Egy lefuttatott lépés eredménye — ez kerül majd a statisztika modulba.</summary>
public sealed class TestResult
{
    public required string StepId { get; init; }
    public TestStatus Status { get; set; } = TestStatus.NotRun;
    public string? Message { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public TimeSpan Duration { get; set; }
}