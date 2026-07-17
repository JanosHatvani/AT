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

    /// <summary>
    /// Ha a lokátor több elemre is illik (pl. egy lista minden sorában ugyanaz az
    /// AutomationId/Id/Name/ClassName ismétlődik), ez választja ki, hányadik találattal
    /// dolgozzon a lépés — 1-alapú, EMBERI számozás (1 = első elem, 2 = második, stb.).
    /// Null vagy 1 esetén az első találat.
    /// </summary>
    public int? ElementIndex { get; set; }

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Ha true, a lépés hibája nem szakítja meg a futtatást — a lista folytatódik.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Ha true, a lépést a futtatás átugorja — meg sem kísérli végrehajtani.</summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Hiba esetén ennyiszer próbálja újra a lépést, mielőtt véglegesen hibásnak jelölné —
    /// 0 esetén nincs retry (a lépés egyszeri hiba esetén azonnal Failed lesz, ahogy eddig is).
    /// Minden kivétel retry-t vált ki (nem csak "elem nem található"-típusú hibák), mert egy
    /// felügyelet nélküli, CI-szerű futtatásnál a legtöbb hiba amúgy is időzítés-érzékeny
    /// (lassú betöltés, animáció, hálózati késés), és a retry ezekre ad esélyt.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// "Self-healing" tartalék lokátor — ha az elsődleges (Locator/LocatorType) a
    /// Timeout-on belül nem található, a driver EZZEL próbálkozik újra, mielőtt hibásnak
    /// jelölné a lépést. Hasznos, ha egy fejlesztő átnevezett egy AutomationId-t/
    /// resource-id-t/Id-t, és nem szeretnéd emiatt azonnal újra felvenni (vagy manuálisan
    /// javítani) az összes érintett tesztlépést — elég egy második, alternatív lokátort
    /// megadni ugyanahhoz az elemhez (pl. Name vagy XPath alapján). Null/üres esetén a
    /// viselkedés változatlan: csak az elsődleges lokátorral próbálkozik.
    /// </summary>
    public string? FallbackLocator { get; set; }
    public LocatorType FallbackLocatorType { get; set; } = LocatorType.Id;

    /// <summary>
    /// A lépés saját, egyedi azonosító címkéje — más lépések ezt hivatkozzák ugrás
    /// célpontjaként (OnSuccessGoToLabel / OnFailureGoToLabel). Automatikusan generált
    /// ("Lépés 1", "Lépés 2", ...), de a felhasználó felülírhatja. Névhez (nem sorszámhoz)
    /// kötött, hogy a lépések átrendezése ne törje el a rá mutató ugrásokat.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// Ha a lépés sikeresen lefut, és ez ki van töltve, a végrehajtás az ezzel a
    /// Label-lel rendelkező lépésre ugrik (nem a listában következőre). Üresen hagyva
    /// a normál, soron következő lépés jön.
    /// </summary>
    public string? OnSuccessGoToLabel { get; set; }

    /// <summary>
    /// Ha a lépés hibára fut, és ez ki van töltve, a végrehajtás az ezzel a Label-lel
    /// rendelkező lépésre ugrik, FÜGGETLENÜL a ContinueOnError beállítástól. Üresen
    /// hagyva a régi viselkedés érvényes: ContinueOnError szerint folytatódik a
    /// következő lépéssel, vagy leáll a futtatás.
    /// </summary>
    public string? OnFailureGoToLabel { get; set; }
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
