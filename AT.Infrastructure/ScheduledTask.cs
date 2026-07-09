using AT.Core.Models;

namespace AT.Infrastructure;

/// <summary>Az ismétlődés típusa — ez dönti el, hogy a nap-specifikáció mezők közül melyik számít.</summary>
public enum ScheduleCadence
{
    Hourly,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Egy elmentett, ütemezhető tesztfeladat — a hozzá tartozó teljes lépéssor beágyazva
/// tárolódik (nincs külső XML-fájl-hivatkozás), így az ütemezés önmagában is
/// hordozza mindazt, ami a futtatáshoz kell. A SchedulerService ebből számolja ki,
/// mikor esedékes legközelebb, és ez hívja meg a megfelelő modul végrehajtó útvonalát.
/// </summary>
public sealed class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>A teszt neve — megjelenítésre és a history/riport összegzésben.</summary>
    public string Name { get; set; } = "";

    /// <summary>A teszt-kategória Id-ja (TestCategory.Id) — kötelező, a teszt "fej" adata,
    /// nem lépésenkénti. Az Ütemezett feladatok és Előzmények nézetek platform+kategória
    /// szerinti szűrője ez alapján csoportosít.</summary>
    public string CategoryId { get; set; } = "";

    /// <summary>Melyik modul futtassa (Web / Desktop / Android).</summary>
    public AutomationTarget Target { get; set; }

    /// <summary>A beágyazott, teljes lépéssor.</summary>
    public List<TestStep> Steps { get; set; } = new();

    /// <summary>
    /// Web modul esetén a futtatáshoz használt böngésző neve (pl. "Chrome", "Firefox", "Edge") —
    /// más modulnál figyelmen kívül marad. SZÁNDÉKOSAN string, nem BrowserType: ez a fájl az
    /// AT.Infrastructure projektben van, aminek nem szabad (és nem is kell) hivatkoznia az
    /// AT.Automation.Web projektre. A string <-> BrowserType átalakítást az AT.App.Services
    /// réteg végzi (lásd TestExecutionService, ScheduleTaskDialog), ahol a BrowserType enum
    /// amúgy is elérhető.
    /// </summary>
    public string? Browser { get; set; }

    public ScheduleCadence Cadence { get; set; }

    /// <summary>A futtatás órája (0–23), helyi idő szerint.</summary>
    public int Hour { get; set; }

    /// <summary>A futtatás perce (0–59), helyi idő szerint.</summary>
    public int Minute { get; set; }

    /// <summary>
    /// Weekly esetén a hét mely napjain fusson le (DayOfWeek 0=Vasárnap..6=Szombat).
    /// Más cadence-nél figyelmen kívül marad.
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    /// <summary>Monthly esetén a hónap hányadik napján fusson le (1–31). Más cadence-nél figyelmen kívül marad.</summary>
    public int DayOfMonth { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// A következő esedékesség — a SchedulerService számolja ki és tartja karban minden
    /// létrehozáskor, szerkesztéskor és lefutás után. Perzisztálva van, hogy program-
    /// újraindítás után is azonnal látható legyen a felületen, kiszámítás nélkül is.
    /// </summary>
    public DateTime? NextRunAt { get; set; }
}
